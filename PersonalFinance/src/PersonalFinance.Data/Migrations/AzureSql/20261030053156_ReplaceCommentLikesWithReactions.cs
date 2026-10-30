using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class ReplaceCommentLikesWithReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StrategyId = table.Column<int>(type: "int", nullable: true),
                    CommentId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityReactions", x => x.Id);
                    table.CheckConstraint("CK_CommunityReactions_OneTarget", "([StrategyId] IS NULL AND [CommentId] IS NOT NULL) OR ([StrategyId] IS NOT NULL AND [CommentId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_CommunityReactions_CommunityComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunityReactions_CommunityStrategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "CommunityStrategies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_CommentId_Kind",
                table: "CommunityReactions",
                columns: new[] { "CommentId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_StrategyId_Kind",
                table: "CommunityReactions",
                columns: new[] { "StrategyId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_UserId_CommentId_Kind",
                table: "CommunityReactions",
                columns: new[] { "UserId", "CommentId", "Kind" },
                unique: true,
                filter: "[CommentId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_UserId_StrategyId_Kind",
                table: "CommunityReactions",
                columns: new[] { "UserId", "StrategyId", "Kind" },
                unique: true,
                filter: "[StrategyId] IS NOT NULL AND [IsDeleted] = 0");

            // Every live like becomes a "Cheering you on" Reaction (kind 2) on the same Comment, by the same Account.
            migrationBuilder.Sql("""
                INSERT INTO [CommunityReactions] ([Kind], [StrategyId], [CommentId], [UserId], [CreatedAt], [UpdatedAt], [IsDeleted], [DeletedAt])
                SELECT 2, NULL, [CommentId], [UserId], [CreatedAt], NULL, 0, NULL
                FROM [CommunityCommentLikes]
                WHERE [IsDeleted] = 0;
                """);

            migrationBuilder.DropTable(
                name: "CommunityCommentLikes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityCommentLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityCommentLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityCommentLikes_CommunityComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "CommunityComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityCommentLikes_CommentId",
                table: "CommunityCommentLikes",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityCommentLikes_UserId_CommentId",
                table: "CommunityCommentLikes",
                columns: new[] { "UserId", "CommentId" },
                unique: true);

            // Live "Cheering you on" Reactions on Comments become likes again; Helpful and Strategy Reactions are lost.
            migrationBuilder.Sql("""
                INSERT INTO [CommunityCommentLikes] ([CommentId], [CreatedAt], [CreatedAtUtc], [DeletedAt], [IsDeleted], [UpdatedAt], [UserId])
                SELECT [CommentId], [CreatedAt], [CreatedAt], NULL, 0, NULL, [UserId]
                FROM [CommunityReactions]
                WHERE [CommentId] IS NOT NULL AND [Kind] = 2 AND [IsDeleted] = 0;
                """);

            migrationBuilder.DropTable(
                name: "CommunityReactions");
        }
    }
}
