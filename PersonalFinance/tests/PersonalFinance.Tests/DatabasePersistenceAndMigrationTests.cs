using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
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
    [InlineData("InMemory", DatabaseProviderType.InMemory)]
    public void DetermineProvider_ExplicitConfigurationKey_ReturnsExpectedProvider(string providerName, DatabaseProviderType expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Database:Provider", providerName }
            })
            .Build();

        var result = DatabaseProviderHelper.DetermineProvider(config, null);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveConnectionString_SqlServer_PreservesNativeConnectionString()
    {
        const string sqlServerConn = "Server=tcp:myserver.database.windows.net,1433;Initial Catalog=myDb;User ID=admin;";
        var resolved = DatabaseProviderHelper.ResolveConnectionString(sqlServerConn);
        Assert.Equal(sqlServerConn, resolved);
    }

    [Fact]
    public void ResolveConnectionString_Empty_ReturnsDefaultSqlConnectionString()
    {
        var resolved = DatabaseProviderHelper.ResolveConnectionString(null);
        Assert.Equal(DatabaseProviderHelper.DefaultSqlConnectionString, resolved);
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
    public void AppDbContextFactory_CreateDbContext_ConfiguresSqlServerProvider()
    {
        var factory = new AppDbContextFactory();
        var context = factory.CreateDbContext([
            "--connection-string",
            "Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;"
        ]);

        Assert.NotNull(context);
        Assert.True(context.Database.IsSqlServer());
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
        var dbName = $"InMemory_DataProtection_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // First container instance writes DataProtection key
        using (var db1 = new AppDbContext(options))
        {
            db1.DataProtectionKeys.Add(new DataProtectionKey
            {
                FriendlyName = "key-instance-1",
                Xml = "<key id=\"test\"><descriptor>secret-xml</descriptor></key>"
            });
            db1.SaveChanges();
        }

        // Second container instance (e.g. after scale-to-zero or container restart) reads DataProtection key
        using (var db2 = new AppDbContext(options))
        {
            var key = db2.DataProtectionKeys.FirstOrDefault(k => k.FriendlyName == "key-instance-1");
            Assert.NotNull(key);
            Assert.Contains("secret-xml", key.Xml);
        }
    }

    [Fact]
    public async Task MigrateAndSeedDatabaseAsync_InMemory_SeedsInitialItems()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppDbContext($"InMemory_Seed_{Guid.NewGuid():N}", DatabaseProviderType.InMemory);

        using (var sp = services.BuildServiceProvider())
        {
            await sp.MigrateAndSeedDatabaseAsync(NullLogger.Instance);

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var items = await db.Items.ToListAsync();
            Assert.Equal(3, items.Count);
            Assert.Contains(items, i => i.Name.Contains("Refit"));
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
    public async Task AppDbContext_ItemsCRUD_PersistsAndUpdatesSuccessfully()
    {
        var dbName = $"InMemory_ItemsCRUD_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using (var db = new AppDbContext(options))
        {
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

        using (var db = new AppDbContext(options))
        {
            var item = await db.Items.FirstAsync(i => i.Name == "Groceries");
            Assert.False(item.IsCompleted);

            item.IsCompleted = true;
            await db.SaveChangesAsync();
        }

        using (var db = new AppDbContext(options))
        {
            var item = await db.Items.FirstAsync(i => i.Name == "Groceries");
            Assert.True(item.IsCompleted);
        }
    }

    [Fact]
    public void SqlServer_DefaultFallbackConnectionString_MatchesDefaultSqlConnectionString()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddAppDbContext(config);

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connectionString = db.Database.GetConnectionString();
        Assert.NotNull(connectionString);
        Assert.Contains("localhost", connectionString);
        Assert.Contains("PersonalFinance", connectionString);
    }

    [Fact]
    public void SqlServer_MigrationSourceFiles_Exist()
    {
        var migrationsDir = Path.Combine(
            DatabasePathHelper.GetSolutionRoot(),
            "PersonalFinance",
            "src",
            "PersonalFinance.Data",
            "Migrations",
            "SqlServer");

        Assert.True(Directory.Exists(migrationsDir));
        Assert.NotEmpty(Directory.GetFiles(migrationsDir, "*.cs"));
    }
}
