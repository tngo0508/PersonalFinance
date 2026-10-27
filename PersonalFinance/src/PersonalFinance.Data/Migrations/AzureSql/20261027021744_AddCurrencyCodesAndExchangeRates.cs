using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddCurrencyCodesAndExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportingCurrency",
                table: "UserPreferences",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "UserBudgetAllocations",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "SpreadsheetTransactionRecords",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "SpreadsheetBudgetSnapshots",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "SavingsGoals",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "PriorityWalks",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "NetWorthParameters",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "MonthlyBudgetLines",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "GoogleDriveConnections",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "BudgetTransactions",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_BaseCurrency_QuoteCurrency_Date",
                table: "ExchangeRates",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "ReportingCurrency",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "PriorityWalks");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "NetWorthParameters");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "MonthlyBudgetLines");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "GoogleDriveConnections");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "BudgetTransactions");
        }
    }
}
