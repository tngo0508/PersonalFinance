using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddAuditableEntityAndSoftDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "SavingsGoals");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "SavingsGoals",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserBudgetAllocations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserBudgetAllocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserBudgetAllocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserBudgetAllocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SpreadsheetTransactionRecords",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SpreadsheetTransactionRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SpreadsheetTransactionRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SpreadsheetTransactionRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SpreadsheetBudgetSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SpreadsheetBudgetSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SpreadsheetBudgetSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SpreadsheetBudgetSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SavingsGoals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SavedArticles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SavedArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SavedArticles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SavedArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "NetWorthParameters",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "NetWorthParameters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "NetWorthParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "NetWorthParameters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "GoogleDriveConnections",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "GoogleDriveConnections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "GoogleDriveConnections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "GoogleDriveConnections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CommunityStoryLikes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CommunityStoryLikes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityStoryLikes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CommunityStoryLikes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CommunityStories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CommunityStories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityStories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CommunityStories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CommunityPollVotes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CommunityPollVotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityPollVotes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CommunityPollVotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CommunityComments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CommunityComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityComments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CommunityComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CommunityCommentLikes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CommunityCommentLikes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityCommentLikes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CommunityCommentLikes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserBudgetAllocations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SpreadsheetTransactionRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SpreadsheetBudgetSnapshots");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SavedArticles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SavedArticles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SavedArticles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SavedArticles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "NetWorthParameters");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "NetWorthParameters");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "NetWorthParameters");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "NetWorthParameters");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "GoogleDriveConnections");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GoogleDriveConnections");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "GoogleDriveConnections");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "GoogleDriveConnections");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CommunityStoryLikes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CommunityStoryLikes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityStoryLikes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CommunityStoryLikes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CommunityPollVotes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CommunityPollVotes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityPollVotes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CommunityPollVotes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CommunityCommentLikes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CommunityCommentLikes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityCommentLikes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CommunityCommentLikes");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "SavingsGoals",
                newName: "UpdatedAtUtc");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
