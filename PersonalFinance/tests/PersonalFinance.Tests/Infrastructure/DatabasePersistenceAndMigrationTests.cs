using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

public class DatabasePersistenceAndMigrationTests
{
    [Theory]
    [InlineData("AzureSql", DatabaseProviderType.AzureSql)]
    [InlineData("MSSQL", DatabaseProviderType.AzureSql)]
    [InlineData("SqlAzure", DatabaseProviderType.AzureSql)]
    [InlineData("InMemory", DatabaseProviderType.InMemory)]
    public void DetermineProvider_ExplicitConfigurationKey_ReturnsExpectedProvider(string providerName, DatabaseProviderType expected)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Database:Provider", providerName }
            })
            .Build();

        DatabaseProviderType result = DatabaseProviderHelper.DetermineProvider(config, null);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveConnectionString_AzureSql_PreservesNativeConnectionString()
    {
        const string sqlServerConn = "Server=tcp:myserver.database.windows.net,1433;Initial Catalog=myDb;User ID=admin;";
        string resolved = DatabaseProviderHelper.ResolveConnectionString(sqlServerConn);
        Assert.Equal(sqlServerConn, resolved);
    }

    [Fact]
    public void ResolveConnectionString_Empty_ReturnsDefaultSqlConnectionString()
    {
        string resolved = DatabaseProviderHelper.ResolveConnectionString(null);
        Assert.Equal(DatabaseProviderHelper.DefaultSqlConnectionString, resolved);
    }

    [Fact]
    public void AddAppDbContext_RegistersAzureSqlImplementation_WhenProviderIsAzureSql()
    {
        AssertRegistersAzureSqlDbContext(new Dictionary<string, string?>
        {
            {
                "ConnectionStrings:DefaultConnection",
                "Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;"
            },
            { "Database:Provider", "AzureSql" }
        });
    }

    private const string LocalSqlServer =
        "Server=localhost,14333;Database=PersonalFinance;User ID=sa;Password=not-a-real-password;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=False";
    private const string AzureSql =
        "Server=tcp:myserver.database.windows.net,1433;Initial Catalog=PersonalFinance;User ID=admin;Password=x;Encrypt=True;TrustServerCertificate=False;";

    // ParseProviderName, not DetermineProvider: with no provider configured, DetermineProvider reads DATABASE_PROVIDER,
    // which the documented InMemory workflow sets in the developer's shell (#350).
    [Theory]
    [InlineData(LocalSqlServer)]
    [InlineData(AzureSql)]
    public void ASqlServerConnectionString_WithNoProviderNamed_SelectsAzureSql(string connectionString)
    {
        Assert.Equal(DatabaseProviderType.AzureSql, DatabaseProviderHelper.ParseProviderName(null, connectionString));
    }

    [Fact]
    public void LocalSqlServer_AndAzureSql_RegisterTheSameMigrationSet()
    {
        List<string> localMigrations = RegisteredMigrations(LocalSqlServer);
        List<string> azureMigrations = RegisteredMigrations(AzureSql);

        Assert.NotEmpty(localMigrations);
        Assert.Equal(azureMigrations, localMigrations);
    }

    private static List<string> RegisteredMigrations(string connectionString) =>
        AssertRegistersAzureSqlDbContext(new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", connectionString },
            { "Database:Provider", "AzureSql" }
        }).Migrations;

    /// <summary>
    /// Registers the app DbContext from the given settings, asserts it resolves to the
    /// Azure SQL (SQL Server) implementation, and returns the configuration and migration set.
    /// </summary>
    private static (IConfiguration Configuration, List<string> Migrations) AssertRegistersAzureSqlDbContext(
        Dictionary<string, string?> settings)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddAppDbContext(config);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        AppDbContext? dbContext = scope.ServiceProvider.GetService<AppDbContext>();

        Assert.NotNull(dbContext);
        Assert.IsType<AzureSqlAppDbContext>(dbContext);
        Assert.True(dbContext.Database.IsSqlServer());

        return (config, dbContext.Database.GetMigrations().ToList());
    }

    [Fact]
    public void AppDbContextFactory_CreateDbContext_ConfiguresAzureSqlProvider()
    {
        var factory = new AppDbContextFactory();
        AppDbContext context = factory.CreateDbContext([
            "--connection-string",
            "Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;"
        ]);

        Assert.NotNull(context);
        Assert.True(context.Database.IsSqlServer());
    }

    [Fact]
    public void AzureSqlAppDbContextFactory_CreateDbContext_ConfiguresAzureSqlProvider()
    {
        var factory = new AzureSqlAppDbContextFactory();
        AzureSqlAppDbContext context = factory.CreateDbContext([
            "--connection-string",
            "Server=localhost;Database=PersonalFinance;Trusted_Connection=True;TrustServerCertificate=True;"
        ]);

        Assert.NotNull(context);
        Assert.True(context.Database.IsSqlServer());
    }

    [Fact]
    public void DataProtectionKeys_PersistAndRetrieveAcrossContextInstances_SimulatesContainerRestart()
    {
        string dbName = $"InMemory_DataProtection_{Guid.NewGuid():N}";
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
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
            DataProtectionKey? key = db2.DataProtectionKeys.FirstOrDefault(k => k.FriendlyName == "key-instance-1");
            Assert.NotNull(key);
            Assert.Contains("secret-xml", key.Xml);
        }
    }

    [Fact]
    public async Task MigrateAndSeedDatabaseAsync_InMemory_SeedsNoCommunityContent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppDbContext($"InMemory_Seed_{Guid.NewGuid():N}", DatabaseProviderType.InMemory);

        using (ServiceProvider sp = services.BuildServiceProvider())
        {
            await sp.MigrateAndSeedDatabaseAsync(NullLogger.Instance);

            using IServiceScope scope = sp.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.False(await db.CommunityPolls.AnyAsync());
            Assert.False(await db.CommunityStrategies.AnyAsync());
        }
    }

    [Fact]
    public void AzureSql_MigrationScript_UsesBoundedKeyTypes_NotNvarcharMax()
    {
        // Generate Azure SQL migration SQL without a live server. This is the strongest in-repo
        // verification that Azure SQL migrations are production-safe.
        const string connectionString =
            "Server=localhost;Database=PersonalFinance_ScriptCheck;Trusted_Connection=True;TrustServerCertificate=True;";

        DbContextOptions<AzureSqlAppDbContext> options = new DbContextOptionsBuilder<AzureSqlAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new AzureSqlAppDbContext(options);
        IMigrator migrator = context.GetService<IMigrator>();
        string script = migrator.GenerateScript(fromMigration: null, toMigration: null, MigrationsSqlGenerationOptions.Idempotent);

        Assert.False(string.IsNullOrWhiteSpace(script));
        Assert.Contains("CREATE TABLE [AspNetRoles]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AspNetUsers]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [DataProtectionKeys]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [GoogleDriveConnections]", script, StringComparison.Ordinal);

        // Identity / FK key columns must be bounded for Azure SQL PK/FK/index support
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

        // Must not leak SQLite store types into Azure SQL scripts
        Assert.DoesNotContain("[Id] TEXT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("INTEGER NOT NULL", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AzureSql_Model_IdentityKeysHaveMaxLength450()
    {
        const string connectionString =
            "Server=localhost;Database=PersonalFinance_ModelCheck;Trusted_Connection=True;TrustServerCertificate=True;";

        DbContextOptions<AzureSqlAppDbContext> options = new DbContextOptionsBuilder<AzureSqlAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new AzureSqlAppDbContext(options);
        IModel model = context.Model;

        IProperty userId = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUser))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityUser.Id))!;
        IProperty roleId = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityRole))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityRole.Id))!;
        IProperty loginProvider = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>.LoginProvider))!;
        IProperty providerKey = model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>))!
            .FindProperty(nameof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>.ProviderKey))!;

        Assert.Equal(450, userId.GetMaxLength());
        Assert.Equal(450, roleId.GetMaxLength());
        Assert.Equal(128, loginProvider.GetMaxLength());
        Assert.Equal(128, providerKey.GetMaxLength());
    }


    [Fact]
    public void AzureSql_BlankConnectionString_FallsBackToTheDefaultSqlConnectionString()
    {
        // Both settings are configured, so AddAppDbContext reads neither DATABASE_PROVIDER nor
        // ConnectionStrings__DefaultConnection from the developer's environment (#350).
        var services = new ServiceCollection();
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "" },
                { "Database:Provider", "AzureSql" }
            })
            .Build();
        services.AddAppDbContext(config);

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Compare parsed forms: SqlClient renames the keywords (Server becomes Data Source) and EF adds an Application Name.
        var registered = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        registered.Remove("Application Name");
        Assert.Equal(new SqlConnectionStringBuilder(DatabaseProviderHelper.DefaultSqlConnectionString).ConnectionString,
            registered.ConnectionString);
    }
}
