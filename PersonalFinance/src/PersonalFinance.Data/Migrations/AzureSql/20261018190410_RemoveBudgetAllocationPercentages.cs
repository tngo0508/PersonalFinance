using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class RemoveBudgetAllocationPercentages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebtRepaymentPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "EntertainmentPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "FoodPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "HealthcarePercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "HousingPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "InsurancePercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "MiscellaneousPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "SavingsPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "TransportationPercent",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "UtilitiesPercent",
                table: "UserBudgetAllocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DebtRepaymentPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EntertainmentPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HealthcarePercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HousingPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsurancePercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MiscellaneousPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SavingsPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransportationPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UtilitiesPercent",
                table: "UserBudgetAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill percentages from amounts
            migrationBuilder.Sql(@"
                UPDATE UserBudgetAllocations SET
                    HousingPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(HousingAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    FoodPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(FoodAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    TransportationPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(TransportationAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    UtilitiesPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(UtilitiesAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    InsurancePercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(InsuranceAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    HealthcarePercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(HealthcareAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    SavingsPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(SavingsAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    DebtRepaymentPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(DebtRepaymentAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    EntertainmentPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(EntertainmentAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END,
                    MiscellaneousPercent = CASE WHEN MonthlyIncome > 0 THEN ROUND(MiscellaneousAmount * 100.0 / MonthlyIncome, 2) ELSE 0 END
            ");
        }
    }
}
