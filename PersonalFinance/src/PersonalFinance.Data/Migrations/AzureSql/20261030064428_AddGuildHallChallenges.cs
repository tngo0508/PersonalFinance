using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddGuildHallChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Habit = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityChallengeMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChallengeId = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityChallengeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityChallengeMembers_CommunityChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "CommunityChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChallengeId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityCheckIns_CommunityChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "CommunityChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityChallengeMembers_ChallengeId",
                table: "CommunityChallengeMembers",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityChallengeMembers_UserId_ChallengeId",
                table: "CommunityChallengeMembers",
                columns: new[] { "UserId", "ChallengeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityChallenges_EndDate",
                table: "CommunityChallenges",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityCheckIns_ChallengeId_PeriodStart",
                table: "CommunityCheckIns",
                columns: new[] { "ChallengeId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityCheckIns_UserId_ChallengeId_PeriodStart",
                table: "CommunityCheckIns",
                columns: new[] { "UserId", "ChallengeId", "PeriodStart" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityChallengeMembers");

            migrationBuilder.DropTable(
                name: "CommunityCheckIns");

            migrationBuilder.DropTable(
                name: "CommunityChallenges");
        }
    }
}
