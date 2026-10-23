using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddDeletionRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurgeAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancelTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurgedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletionRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_CancelTokenHash",
                table: "DeletionRequests",
                column: "CancelTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_Status_PurgeAfter",
                table: "DeletionRequests",
                columns: new[] { "Status", "PurgeAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_UserId",
                table: "DeletionRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletionRequests");
        }
    }
}
