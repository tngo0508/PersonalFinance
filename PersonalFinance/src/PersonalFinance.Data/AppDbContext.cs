using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Data;

/// <summary>
/// Database context inheriting from IdentityDbContext for ASP.NET Core Identity authentication tables
/// and implementing IDataProtectionKeyContext for persistent Data Protection keyring across container restarts.
/// </summary>
public class AppDbContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    /// <summary>
    /// Non-generic options constructor so provider-specific subclasses can forward their typed options.
    /// </summary>
    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Items table set.
    /// </summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>
    /// User Google Drive connections configuration table set.
    /// </summary>
    public DbSet<GoogleDriveConnection> GoogleDriveConnections => Set<GoogleDriveConnection>();

    /// <summary>
    /// Locally cached Google Drive files and metadata table set.
    /// </summary>
    public DbSet<GoogleDriveCachedFile> GoogleDriveCachedFiles => Set<GoogleDriveCachedFile>();

    /// <summary>
    /// ASP.NET Core Data Protection keys table set for preserving cookie/token encryption across restarts.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit Identity key/FK lengths so both providers (and design-time scaffolding) produce
        // bounded string columns. SQL Server cannot index or key nvarchar(max).
        const int identityKeyMaxLength = 450;
        const int identityLoginTokenMaxLength = 128;

        modelBuilder.Entity<IdentityUser>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityRole>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.Property(e => e.LoginProvider).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.ProviderKey).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
            entity.Property(e => e.RoleId).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
            entity.Property(e => e.LoginProvider).HasMaxLength(identityLoginTokenMaxLength);
            entity.Property(e => e.Name).HasMaxLength(identityLoginTokenMaxLength);
        });

        modelBuilder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(identityKeyMaxLength);
        });

        modelBuilder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.Property(e => e.RoleId).HasMaxLength(identityKeyMaxLength);
        });

        // Configure Item entity schema constraints
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        // Configure GoogleDriveConnection entity
        modelBuilder.Entity<GoogleDriveConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FolderId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FolderUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.EncryptedApiKey).HasMaxLength(2000);
            entity.Property(e => e.MaskedApiKey).HasMaxLength(100);
            entity.Property(e => e.SyncStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.FolderId });

            entity.HasMany(e => e.CachedFiles)
                .WithOne(e => e.Connection)
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure GoogleDriveCachedFile entity
        modelBuilder.Entity<GoogleDriveCachedFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DriveFileId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MimeType).HasMaxLength(250);
            entity.Property(e => e.SizeFormatted).HasMaxLength(50);
            entity.Property(e => e.FileType).HasMaxLength(100);
            entity.Property(e => e.IconBadgeClass).HasMaxLength(50);
            entity.Property(e => e.WebViewLink).HasMaxLength(1000);
            entity.Property(e => e.IconLink).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailLink).HasMaxLength(1000);

            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => new { e.ConnectionId, e.DriveFileId });
        });
    }
}

/// <summary>
/// SQL Server / Azure SQL-specific AppDbContext used for EF Core migrations under <c>Migrations/SqlServer</c>
/// and as the DI implementation when the active provider is SQL Server.
/// </summary>
public sealed class SqlServerAppDbContext : AppDbContext
{
    public SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options) : base(options)
    {
    }
}
