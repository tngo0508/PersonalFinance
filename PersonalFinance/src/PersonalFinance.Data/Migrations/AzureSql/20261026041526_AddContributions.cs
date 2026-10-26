using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HighestMilestoneNotified",
                table: "SavingsGoals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Contributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SavingsGoalId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contributions_SavingsGoals_SavingsGoalId",
                        column: x => x.SavingsGoalId,
                        principalTable: "SavingsGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_SavingsGoalId",
                table: "Contributions",
                column: "SavingsGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId_SavingsGoalId_Date",
                table: "Contributions",
                columns: new[] { "UserId", "SavingsGoalId", "Date" });

            // Backfill: each goal's saved amount becomes one Opening Contribution (Kind 1), dated the goal's creation,
            // so nothing a user entered is lost. Soft-deleted goals get one too, hidden the same way, so cancelling a
            // pending Deletion Request restores the goal and its Contribution together.
            migrationBuilder.Sql(@"
INSERT INTO [Contributions] ([SavingsGoalId], [Amount], [Date], [Note], [Kind], [UserId], [CreatedAt], [UpdatedAt], [IsDeleted], [DeletedAt])
SELECT [Id], [CurrentAmount], CAST([CreatedAt] AS date), NULL, 1, [UserId], [CreatedAt], NULL, [IsDeleted], [DeletedAt]
FROM [SavingsGoals]
WHERE [CurrentAmount] <> 0;");

            // Milestones already passed are marked notified, so none fires again; Reached is now derived from the
            // saved amount.
            migrationBuilder.Sql(@"
UPDATE [SavingsGoals]
SET [HighestMilestoneNotified] = CASE
        WHEN [TargetAmount] <= 0 THEN 0
        WHEN [CurrentAmount] >= [TargetAmount] THEN 100
        WHEN [CurrentAmount] * 4 >= [TargetAmount] * 3 THEN 75
        WHEN [CurrentAmount] * 2 >= [TargetAmount] THEN 50
        ELSE 0
    END,
    [IsCompleted] = CASE WHEN [TargetAmount] > 0 AND [CurrentAmount] >= [TargetAmount] THEN 1 ELSE 0 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contributions");

            migrationBuilder.DropColumn(
                name: "HighestMilestoneNotified",
                table: "SavingsGoals");
        }
    }
}
