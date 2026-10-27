using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddCommunityPseudonyms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "CommunityComments");

            migrationBuilder.CreateTable(
                name: "CommunityPseudonyms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPseudonyms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPseudonyms_Name",
                table: "CommunityPseudonyms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPseudonyms_UserId",
                table: "CommunityPseudonyms",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityPseudonyms");

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "CommunityStories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "CommunityComments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
