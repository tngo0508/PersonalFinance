using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class MakeGoogleDriveCachedFileConnectionOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoogleDriveCachedFiles_GoogleDriveConnections_ConnectionId",
                table: "GoogleDriveCachedFiles");

            migrationBuilder.AlterColumn<int>(
                name: "ConnectionId",
                table: "GoogleDriveCachedFiles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_GoogleDriveCachedFiles_GoogleDriveConnections_ConnectionId",
                table: "GoogleDriveCachedFiles",
                column: "ConnectionId",
                principalTable: "GoogleDriveConnections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoogleDriveCachedFiles_GoogleDriveConnections_ConnectionId",
                table: "GoogleDriveCachedFiles");

            migrationBuilder.AlterColumn<int>(
                name: "ConnectionId",
                table: "GoogleDriveCachedFiles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GoogleDriveCachedFiles_GoogleDriveConnections_ConnectionId",
                table: "GoogleDriveCachedFiles",
                column: "ConnectionId",
                principalTable: "GoogleDriveConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
