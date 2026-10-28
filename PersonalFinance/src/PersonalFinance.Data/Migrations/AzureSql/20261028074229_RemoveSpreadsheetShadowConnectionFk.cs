using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class RemoveSpreadsheetShadowConnectionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpreadsheetBudgetSnapshots_GoogleDriveConnections_GoogleDriveConnectionId",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_SpreadsheetTransactionRecords_GoogleDriveConnections_GoogleDriveConnectionId",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropIndex(
                name: "IX_SpreadsheetTransactionRecords_GoogleDriveConnectionId",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropIndex(
                name: "IX_SpreadsheetBudgetSnapshots_GoogleDriveConnectionId",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropColumn(
                name: "GoogleDriveConnectionId",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropColumn(
                name: "GoogleDriveConnectionId",
                table: "SpreadsheetBudgetSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoogleDriveConnectionId",
                table: "SpreadsheetTransactionRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoogleDriveConnectionId",
                table: "SpreadsheetBudgetSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpreadsheetTransactionRecords_GoogleDriveConnectionId",
                table: "SpreadsheetTransactionRecords",
                column: "GoogleDriveConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpreadsheetBudgetSnapshots_GoogleDriveConnectionId",
                table: "SpreadsheetBudgetSnapshots",
                column: "GoogleDriveConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpreadsheetBudgetSnapshots_GoogleDriveConnections_GoogleDriveConnectionId",
                table: "SpreadsheetBudgetSnapshots",
                column: "GoogleDriveConnectionId",
                principalTable: "GoogleDriveConnections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SpreadsheetTransactionRecords_GoogleDriveConnections_GoogleDriveConnectionId",
                table: "SpreadsheetTransactionRecords",
                column: "GoogleDriveConnectionId",
                principalTable: "GoogleDriveConnections",
                principalColumn: "Id");
        }
    }
}
