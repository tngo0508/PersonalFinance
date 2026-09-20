using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleDrivePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleDriveConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FolderId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FolderUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MaskedApiKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleDriveConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoogleDriveCachedFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DriveFileId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: true),
                    SizeFormatted = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WebViewLink = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IconLink = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ThumbnailLink = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IconBadgeClass = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsFolder = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTrashed = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastFetchedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleDriveCachedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleDriveCachedFiles_GoogleDriveConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "GoogleDriveConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveCachedFiles_ConnectionId",
                table: "GoogleDriveCachedFiles",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveCachedFiles_ConnectionId_DriveFileId",
                table: "GoogleDriveCachedFiles",
                columns: new[] { "ConnectionId", "DriveFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveConnections_UserId",
                table: "GoogleDriveConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveConnections_UserId_FolderId",
                table: "GoogleDriveConnections",
                columns: new[] { "UserId", "FolderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleDriveCachedFiles");

            migrationBuilder.DropTable(
                name: "GoogleDriveConnections");
        }
    }
}
