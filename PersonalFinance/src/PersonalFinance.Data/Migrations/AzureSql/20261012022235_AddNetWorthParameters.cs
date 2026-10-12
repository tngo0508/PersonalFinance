using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddNetWorthParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetWorthParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualSavings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualSpending = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentAge = table.Column<int>(type: "int", nullable: false),
                    RetirementAge = table.Column<int>(type: "int", nullable: false),
                    BaseRoi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ConservativeRoi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AggressiveRoi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AggressiveSavings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetWorthParameters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetWorthParameters_UserId",
                table: "NetWorthParameters",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetWorthParameters");
        }
    }
}
