#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonalFinance.Data.Migrations.AzureSql;

/// <inheritdoc />
public partial class UpdateModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "SpreadsheetBudgetSnapshots",
            table => new
            {
                Id = table.Column<int>("int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ConnectionId = table.Column<int>("int", nullable: false),
                UserId = table.Column<string>("nvarchar(450)", maxLength: 450, nullable: false),
                FileName = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
                FileId = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
                MonthNumber = table.Column<int>("int", nullable: false),
                MonthName = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
                Year = table.Column<int>("int", nullable: false),
                BudgetedIncome =
                    table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: false),
                ActualIncome =
                    table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: false),
                BudgetedExpenses =
                    table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: false),
                ActualExpenses =
                    table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: false),
                StartingBalance =
                    table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: true),
                EndingBalance =
                    table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: true),
                CategoryBreakdownJson =
                    table.Column<string>("nvarchar(max)", maxLength: 10000, nullable: false),
                SyncedAtUtc = table.Column<DateTime>("datetime2", nullable: false),
                GoogleDriveConnectionId = table.Column<int>("int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpreadsheetBudgetSnapshots", x => x.Id);
                table.ForeignKey(
                    "FK_SpreadsheetBudgetSnapshots_GoogleDriveConnections_ConnectionId",
                    x => x.ConnectionId,
                    "GoogleDriveConnections",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_SpreadsheetBudgetSnapshots_GoogleDriveConnections_GoogleDriveConnectionId",
                    x => x.GoogleDriveConnectionId,
                    "GoogleDriveConnections",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "SpreadsheetTransactionRecords",
            table => new
            {
                Id = table.Column<int>("int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SnapshotId = table.Column<int>("int", nullable: false),
                ConnectionId = table.Column<int>("int", nullable: false),
                UserId = table.Column<string>("nvarchar(450)", maxLength: 450, nullable: false),
                Date = table.Column<DateTime>("datetime2", nullable: false),
                Amount = table.Column<decimal>("decimal(18,4)", precision: 18, scale: 4, nullable: false),
                Description = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
                Category = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                Type = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
                GoogleDriveConnectionId = table.Column<int>("int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpreadsheetTransactionRecords", x => x.Id);
                table.ForeignKey(
                    "FK_SpreadsheetTransactionRecords_GoogleDriveConnections_ConnectionId",
                    x => x.ConnectionId,
                    "GoogleDriveConnections",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_SpreadsheetTransactionRecords_GoogleDriveConnections_GoogleDriveConnectionId",
                    x => x.GoogleDriveConnectionId,
                    "GoogleDriveConnections",
                    "Id");
                table.ForeignKey(
                    "FK_SpreadsheetTransactionRecords_SpreadsheetBudgetSnapshots_SnapshotId",
                    x => x.SnapshotId,
                    "SpreadsheetBudgetSnapshots",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetBudgetSnapshots_ConnectionId",
            "SpreadsheetBudgetSnapshots",
            "ConnectionId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetBudgetSnapshots_GoogleDriveConnectionId",
            "SpreadsheetBudgetSnapshots",
            "GoogleDriveConnectionId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetBudgetSnapshots_UserId",
            "SpreadsheetBudgetSnapshots",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetBudgetSnapshots_UserId_Year_MonthNumber",
            "SpreadsheetBudgetSnapshots",
            new[] { "UserId", "Year", "MonthNumber" });

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetTransactionRecords_ConnectionId",
            "SpreadsheetTransactionRecords",
            "ConnectionId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetTransactionRecords_GoogleDriveConnectionId",
            "SpreadsheetTransactionRecords",
            "GoogleDriveConnectionId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetTransactionRecords_SnapshotId",
            "SpreadsheetTransactionRecords",
            "SnapshotId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetTransactionRecords_UserId",
            "SpreadsheetTransactionRecords",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetTransactionRecords_UserId_Category_Date",
            "SpreadsheetTransactionRecords",
            new[] { "UserId", "Category", "Date" });

        migrationBuilder.CreateIndex(
            "IX_SpreadsheetTransactionRecords_UserId_Date",
            "SpreadsheetTransactionRecords",
            new[] { "UserId", "Date" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "SpreadsheetTransactionRecords");

        migrationBuilder.DropTable(
            "SpreadsheetBudgetSnapshots");
    }
}