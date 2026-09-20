using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Data;

/// <summary>
/// Database context inheriting from IdentityDbContext for ASP.NET Core Identity authentication tables.
/// </summary>
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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