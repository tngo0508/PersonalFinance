using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddPriorityWalk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriorityWalks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DebtApr = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DebtBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EmergencyFundMonths = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    MonthlyExpenses = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HasFullEmployerMatch = table.Column<bool>(type: "bit", nullable: true),
                    EmployerMatchPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    HorizonYears = table.Column<int>(type: "int", nullable: true),
                    AgeBand = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    AtContributionLimit = table.Column<bool>(type: "bit", nullable: true),
                    TreeVersion = table.Column<int>(type: "int", nullable: false),
                    LimitsTaxYear = table.Column<int>(type: "int", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeafId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PurposeNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityWalks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriorityWalks_UserId_CreatedAt",
                table: "PriorityWalks",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriorityWalks");
        }
    }
}
