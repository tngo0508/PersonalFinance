using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using Xunit;

namespace PersonalFinance.Tests;

public class DatabasePersistenceAndMigrationTests
{
    [Theory]
    [InlineData("SqlServer", DatabaseProviderType.SqlServer)]
    [InlineData("MSSQL", DatabaseProviderType.SqlServer)]
    [InlineData("AzureSql", DatabaseProviderType.SqlServer)]
    [InlineData("SqlAzure", DatabaseProviderType.SqlServer)]
    [InlineData("sqlite", DatabaseProviderType.Sqlite)]
    [InlineData("SQLite", DatabaseProviderType.Sqlite)]
    public void DetermineProvider_ExplicitConfigurationKey_ReturnsExpectedProvider(string providerName, DatabaseProviderType expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Database:Provider", providerName }
            })
            .Build();

        var result = DatabaseProviderHelper.DetermineProvider(config, "Data Source=PersonalFinance.db");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Server=tcp:myserver.database.windows.net,1433;Initial Catalog=myDb;User ID=admin;Password=Secret123!;Encrypt=True;")]
    [InlineData("Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;")]
    [InlineData("Data Source=tcp:sqlserver.internal,1433;Initial Catalog=PersonalFinance;Integrated Security=SSPI;")]
    public void DetectProviderFromConnectionString_SqlServerStrings_ReturnsSqlServer(string connectionString)
    {
        var provider = DatabaseProviderHelper.DetectProviderFromConnectionString(connectionString);
        Assert.Equal(DatabaseProviderType.SqlServer, provider);
    }

    [Theory]
    [InlineData("Data Source=PersonalFinance.db")]
    [InlineData("Data Source=:memory:")]
    [InlineData("DataSource=app.db;Cache=Shared")]
    [InlineData("")]
    [InlineData(null)]
    public void DetectProviderFromConnectionString_SqliteStrings_ReturnsSqlite(string? connectionString)
    {
        var provider = DatabaseProviderHelper.DetectProviderFromConnectionString(connectionString);
        Assert.Equal(DatabaseProviderType.Sqlite, provider);
    }

    [Fact]
    public void ResolveConnectionString_SqlServer_PreservesNativeConnectionString()
    {
        const string sqlServerConn = "Server=tcp:myserver.database.windows.net,1433;Initial Catalog=myDb;User ID=admin;";
        var resolved = DatabaseProviderHelper.ResolveConnectionString(sqlServerConn, DatabaseProviderType.SqlServer);
        Assert.Equal(sqlServerConn, resolved);
    }

    [Fact]
    public void ResolveConnectionString_Sqlite_AnchorsToSolutionDirectory()
    {
        const string sqliteConn = "Data Source=PersonalFinance.db";
        var resolved = DatabaseProviderHelper.ResolveConnectionString(sqliteConn, DatabaseProviderType.Sqlite);
        Assert.Contains("PersonalFinance.db", resolved);
        Assert.True(Path.IsPathRooted(resolved.Replace("Data Source=", "").Trim()));
    }

    [Fact]
    public void AddAppDbContext_RegistersSqliteImplementation_WhenProviderIsSqlite()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Data Source=:memory:" },
                { "Database:Provider", "Sqlite" }
            })
            .Build();

        services.AddAppDbContext(config);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<AppDbContext>();

        Assert.NotNull(dbContext);
        Assert.IsType<SqliteAppDbContext>(dbContext);
        Assert.True(dbContext.Database.IsSqlite());
    }

    [Fact]
    public void AddAppDbContext_RegistersSqlServerImplementation_WhenProviderIsSqlServer()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {
                    "ConnectionStrings:DefaultConnection",
                    "Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;"
                },
                { "Database:Provider", "SqlServer" }
            })
            .Build();

        services.AddAppDbContext(config);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<AppDbContext>();

        Assert.NotNull(dbContext);
        Assert.IsType<SqlServerAppDbContext>(dbContext);
        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void AppDbContextFactory_CreateDbContext_WithSqliteArgs_ReturnsSqliteImplementation()
    {
        var factory = new AppDbContextFactory();
        var context = factory.CreateDbContext(["--provider", "Sqlite", "--connection-string", "Data Source=:memory:"]);

        Assert.NotNull(context);
        Assert.IsType<SqliteAppDbContext>(context);
        Assert.True(context.Database.IsSqlite());
    }

    [Fact]
    public void SqlServerAppDbContextFactory_CreateDbContext_ConfiguresSqlServerProvider()
    {
        var factory = new SqlServerAppDbContextFactory();
        var context = factory.CreateDbContext([
            "--connection-string",
            "Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;"
        ]);

        Assert.NotNull(context);
        Assert.True(context.Database.IsSqlServer());
    }

    [Fact]
    public void DataProtectionKeys_PersistAndRetrieveAcrossContextInstances_SimulatesContainerRestart()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite(connection)
            .Options;

        // First container instance writes DataProtection key
        using (var db1 = new SqliteAppDbContext(options))
        {
            db1.Database.EnsureCreated();
            db1.DataProtectionKeys.Add(new DataProtectionKey
            {
                FriendlyName = "key-instance-1",
                Xml = "<key id=\"test\"><descriptor>secret-xml</descriptor></key>"
            });
            db1.SaveChanges();
        }

        // Second container instance (e.g. after scale-to-zero or container restart) reads DataProtection key
        using (var db2 = new SqliteAppDbContext(options))
        {
            var key = db2.DataProtectionKeys.FirstOrDefault(k => k.FriendlyName == "key-instance-1");
            Assert.NotNull(key);
            Assert.Contains("secret-xml", key.Xml);
        }
    }

    [Fact]
    public async Task MigrateAndSeedDatabaseAsync_Sqlite_AppliesProviderMigrationsAndSeedsInitialItems()
    {
        // Use unique file SQLite database to test real migration pipeline against SqliteAppDbContext migrations
        var dbPath = Path.Combine(Path.GetTempPath(), $"pf_migration_test_{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAppDbContext($"Data Source={dbPath}", DatabaseProviderType.Sqlite);

            using (var sp = services.BuildServiceProvider())
            {
                await sp.MigrateAndSeedDatabaseAsync(NullLogger.Instance);

                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                Assert.IsType<SqliteAppDbContext>(db);

                var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
                Assert.NotEmpty(appliedMigrations);
                Assert.Contains(appliedMigrations, m => m.Contains("InitialCreate"));

                var items = await db.Items.ToListAsync();
                Assert.Equal(3, items.Count);
                Assert.Contains(items, i => i.Name.Contains("Refit"));
            }

            // Verify idempotency on second execution (no duplication of items)
            using (var sp2 = services.BuildServiceProvider())
            {
                await sp2.MigrateAndSeedDatabaseAsync(NullLogger.Instance);

                using var scope2 = sp2.CreateScope();
                var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
                var items2 = await db2.Items.ToListAsync();
                Assert.Equal(3, items2.Count);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { /* ignore temp cleanup */ }
            }
        }
    }

    [Fact]
    public void SqlServer_MigrationScript_UsesBoundedKeyTypes_NotNvarcharMax()
    {
        // Generate SQL Server migration SQL without a live server. This is the strongest in-repo
        // verification that Azure SQL migrations are production-safe.
        const string connectionString =
            "Server=localhost;Database=PersonalFinance_ScriptCheck;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new SqlServerAppDbContext(options);
        var migrator = context.GetService<IMigrator>();
        var script = migrator.GenerateScript(fromMigration: null, toMigration: null, MigrationsSqlGenerationOptions.Idempotent);

        Assert.False(string.IsNullOrWhiteSpace(script));
        Assert.Contains("CREATE TABLE [AspNetRoles]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AspNetUsers]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [DataProtectionKeys]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [GoogleDriveConnections]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Items]", script, StringComparison.Ordinal);

        // Identity / FK key columns must be bounded for SQL Server PK/FK/index support
        Assert.Contains("[Id] nvarchar(450)", script, StringComparison.Ordinal);
        Assert.Contains("[UserId] nvarchar(450)", script, StringComparison.Ordinal);
        Assert.Contains("[RoleId] nvarchar(450)", script, StringComparison.Ordinal);
        Assert.Contains("[LoginProvider] nvarchar(128)", script, StringComparison.Ordinal);
        Assert.Contains("[ProviderKey] nvarchar(128)", script, StringComparison.Ordinal);

        // Ensure we never emit the broken unbounded key pattern on PK Id columns
        Assert.DoesNotContain("[Id] nvarchar(max)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[UserId] nvarchar(max)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[RoleId] nvarchar(max)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[LoginProvider] nvarchar(max)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[ProviderKey] nvarchar(max)", script, StringComparison.Ordinal);

        // Must not leak SQLite store types into SQL Server scripts
        Assert.DoesNotContain("[Id] TEXT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("INTEGER NOT NULL", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer_Model_IdentityKeysHaveMaxLength450()
    {
        const string connectionString =
            "Server=localhost;Database=PersonalFinance_ModelCheck;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new SqlServerAppDbContext(options);
        var model = context.Model;

        var userId = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUser))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityUser.Id))!;
        var roleId = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityRole))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityRole.Id))!;
        var loginProvider = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>.LoginProvider))!;
        var providerKey = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>.ProviderKey))!;

        Assert.Equal(450, userId.GetMaxLength());
        Assert.Equal(450, roleId.GetMaxLength());
        Assert.Equal(128, loginProvider.GetMaxLength());
        Assert.Equal(128, providerKey.GetMaxLength());
    }

    [Fact]
    public void Sqlite_And_SqlServer_MigrationSets_AreProviderSpecificAndIsolated()
    {
        var sqliteOptions = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var sqlServerOptions = new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer("Server=localhost;Database=x;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var sqlite = new SqliteAppDbContext(sqliteOptions);
        using var sqlServer = new SqlServerAppDbContext(sqlServerOptions);

        var sqliteMigrations = sqlite.Database.GetMigrations().ToList();
        var sqlServerMigrations = sqlServer.Database.GetMigrations().ToList();

        Assert.NotEmpty(sqliteMigrations);
        Assert.NotEmpty(sqlServerMigrations);
        Assert.All(sqliteMigrations, m => Assert.Contains("InitialCreate", m));
        Assert.All(sqlServerMigrations, m => Assert.Contains("InitialCreate", m));

        // Migration IDs differ because each provider has its own scaffolded history
        Assert.NotEqual(sqliteMigrations, sqlServerMigrations);
    }

    [Fact]
    public async Task AppDbContext_ItemsCRUD_PersistsAndUpdatesSuccessfully()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var db = new SqliteAppDbContext(options))
        {
            db.Database.EnsureCreated();

            var item = new Item
            {
                Name = "Groceries",
                Description = "Weekly supermarket shopping",
                IsCompleted = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Items.Add(item);
            await db.SaveChangesAsync();

            Assert.True(item.Id > 0);
        }

        using (var db = new SqliteAppDbContext(options))
        {
            var item = await db.Items.FirstAsync(i => i.Name == "Groceries");
            Assert.False(item.IsCompleted);

            item.IsCompleted = true;
            await db.SaveChangesAsync();
        }

        using (var db = new SqliteAppDbContext(options))
        {
            var item = await db.Items.FirstAsync(i => i.Name == "Groceries");
            Assert.True(item.IsCompleted);
        }
    }

    [Fact]
    public void ProviderMigrationSourceFiles_ExistForBothProviders()
    {
        var dataProject = Path.Combine(
            DatabasePathHelper.GetSolutionRoot(),
            "PersonalFinance",
            "src",
            "PersonalFinance.Data",
            "Migrations");

        var sqliteDir = Path.Combine(dataProject, "Sqlite");
        var sqlServerDir = Path.Combine(dataProject, "SqlServer");

        Assert.True(Directory.Exists(sqliteDir), "Migrations/Sqlite folder must exist.");
        Assert.True(Directory.Exists(sqlServerDir), "Migrations/SqlServer folder must exist.");

        Assert.NotEmpty(Directory.GetFiles(sqliteDir, "*InitialCreate.cs"));
        Assert.NotEmpty(Directory.GetFiles(sqlServerDir, "*InitialCreate.cs"));
        Assert.True(File.Exists(Path.Combine(sqliteDir, "SqliteAppDbContextModelSnapshot.cs")));
        Assert.True(File.Exists(Path.Combine(sqlServerDir, "SqlServerAppDbContextModelSnapshot.cs")));

        var sqlServerMigration = Directory.GetFiles(sqlServerDir, "*InitialCreate.cs")
            .Single(f => !f.EndsWith("Designer.cs", StringComparison.OrdinalIgnoreCase));
        var content = File.ReadAllText(sqlServerMigration);

        Assert.Contains("nvarchar(450)", content);
        Assert.Contains("maxLength: 450", content);
        Assert.Contains("maxLength: 128", content);
        Assert.DoesNotContain("type: \"TEXT\"", content);
    }
}
