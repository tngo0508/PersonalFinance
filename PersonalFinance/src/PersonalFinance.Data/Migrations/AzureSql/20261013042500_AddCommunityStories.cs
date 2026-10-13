using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddCommunityStories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityStories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Likes = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadTimeMinutes = table.Column<int>(type: "int", nullable: false)
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
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityStoryLikes");

            migrationBuilder.DropTable(
                name: "CommunityStories");
        }
    }
}
