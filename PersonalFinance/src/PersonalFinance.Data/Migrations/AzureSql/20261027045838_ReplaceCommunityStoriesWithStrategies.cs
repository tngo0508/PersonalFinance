using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <summary>
    /// Guild Hall slice 3 (#420): Strategies replace Community stories. The stories and their likes are dropped (the
    /// Guild Hall has been empty since #422), and a comment's story reference becomes a Strategy reference. A comment
    /// that still names a dropped story keeps its text with no target, so the new foreign key has nothing to reject.
    /// </summary>
    public partial class ReplaceCommunityStoriesWithStrategies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityComments_CommunityStories_StoryId",
                table: "CommunityComments");

            migrationBuilder.DropTable(
                name: "CommunityStoryLikes");

            migrationBuilder.DropTable(
                name: "CommunityStories");

            migrationBuilder.RenameColumn(
                name: "StoryId",
                table: "CommunityComments",
                newName: "StrategyId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunityComments_StoryId",
                table: "CommunityComments",
                newName: "IX_CommunityComments_StrategyId");

            migrationBuilder.Sql("UPDATE [CommunityComments] SET [StrategyId] = NULL WHERE [StrategyId] IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "CommunityStrategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tried = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Happened = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Keeping = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityStrategies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStrategies_Status_Category_CreatedAt",
                table: "CommunityStrategies",
                columns: new[] { "Status", "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStrategies_UserId",
                table: "CommunityStrategies",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityComments_CommunityStrategies_StrategyId",
                table: "CommunityComments",
                column: "StrategyId",
                principalTable: "CommunityStrategies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityComments_CommunityStrategies_StrategyId",
                table: "CommunityComments");

            migrationBuilder.DropTable(
                name: "CommunityStrategies");

            migrationBuilder.RenameColumn(
                name: "StrategyId",
                table: "CommunityComments",
                newName: "StoryId");

            migrationBuilder.RenameIndex(
                name: "IX_CommunityComments_StrategyId",
                table: "CommunityComments",
                newName: "IX_CommunityComments_StoryId");

            migrationBuilder.Sql("UPDATE [CommunityComments] SET [StoryId] = NULL WHERE [StoryId] IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "CommunityStories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadTimeMinutes = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityStories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityStoryLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityStoryLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityStoryLikes_CommunityStories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "CommunityStories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStories_Category",
                table: "CommunityStories",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStories_IsFeatured",
                table: "CommunityStories",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStories_PublishedAtUtc",
                table: "CommunityStories",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStoryLikes_StoryId",
                table: "CommunityStoryLikes",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityStoryLikes_UserId_StoryId",
                table: "CommunityStoryLikes",
                columns: new[] { "UserId", "StoryId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityComments_CommunityStories_StoryId",
                table: "CommunityComments",
                column: "StoryId",
                principalTable: "CommunityStories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
