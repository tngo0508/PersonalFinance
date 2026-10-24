using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddResearchSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dataset = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstPeriod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LastPeriod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceAsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    BuiltAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchSnapshots_Dataset",
                table: "ResearchSnapshots",
                column: "Dataset",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchSnapshots");
        }
    }
}
