using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddBudgetAllocationAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DebtRepaymentAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EntertainmentAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HealthcareAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HousingAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MiscellaneousAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SavingsAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransportationAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UtilitiesAmount",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill every existing row, including soft-deleted ones (a plain UPDATE bypasses the EF query filter).
            // EXEC defers compilation so the UPDATE is valid in generated scripts, where it shares a batch with the ALTERs.
            migrationBuilder.Sql(@"
EXEC(N'UPDATE UserBudgetAllocations
SET HousingAmount = ROUND(MonthlyIncome * HousingPercent / 100.0, 2),
    FoodAmount = ROUND(MonthlyIncome * FoodPercent / 100.0, 2),
    TransportationAmount = ROUND(MonthlyIncome * TransportationPercent / 100.0, 2),
    UtilitiesAmount = ROUND(MonthlyIncome * UtilitiesPercent / 100.0, 2),
    InsuranceAmount = ROUND(MonthlyIncome * InsurancePercent / 100.0, 2),
    HealthcareAmount = ROUND(MonthlyIncome * HealthcarePercent / 100.0, 2),
    SavingsAmount = ROUND(MonthlyIncome * SavingsPercent / 100.0, 2),
    DebtRepaymentAmount = ROUND(MonthlyIncome * DebtRepaymentPercent / 100.0, 2),
    EntertainmentAmount = ROUND(MonthlyIncome * EntertainmentPercent / 100.0, 2),
    MiscellaneousAmount = ROUND(MonthlyIncome * MiscellaneousPercent / 100.0, 2)');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebtRepaymentAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "EntertainmentAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "FoodAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "HealthcareAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "HousingAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "InsuranceAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "MiscellaneousAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "SavingsAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "TransportationAmount",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "UtilitiesAmount",
                table: "UserBudgetAllocations");
        }
    }
}
