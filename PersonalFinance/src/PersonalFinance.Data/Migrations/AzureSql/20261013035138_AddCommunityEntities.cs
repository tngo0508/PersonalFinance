using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class AddCommunityEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityPolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PollCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPolls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PollId = table.Column<int>(type: "int", nullable: true),
                    CaseStudyTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Likes = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityComments_CommunityPolls_PollId",
                        column: x => x.PollId,
                        principalTable: "CommunityPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPollOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    PollId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPollOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPollOptions_CommunityPolls_PollId",
                        column: x => x.PollId,
                        principalTable: "CommunityPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommunityCommentLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "CommunityPollVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OptionId = table.Column<int>(type: "int", nullable: true),
                    PollId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPollVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPollVotes_CommunityPollOptions_OptionId",
                        column: x => x.OptionId,
                        principalTable: "CommunityPollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommunityPollVotes_CommunityPolls_PollId",
                        column: x => x.PollId,
                        principalTable: "CommunityPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_CreatedAtUtc",
                table: "CommunityComments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_PollId",
                table: "CommunityComments",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollOptions_PollId",
                table: "CommunityPollOptions",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_CreatedAtUtc",
                table: "CommunityPolls",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_PollCode",
                table: "CommunityPolls",
                column: "PollCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_OptionId",
                table: "CommunityPollVotes",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_PollId",
                table: "CommunityPollVotes",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_UserId_PollId",
                table: "CommunityPollVotes",
                columns: new[] { "UserId", "PollId" },
                unique: true,
                filter: "[PollId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityCommentLikes");

            migrationBuilder.DropTable(
                name: "CommunityPollVotes");

            migrationBuilder.DropTable(
                name: "CommunityComments");

            migrationBuilder.DropTable(
                name: "CommunityPollOptions");

            migrationBuilder.DropTable(
                name: "CommunityPolls");
        }
    }
}
