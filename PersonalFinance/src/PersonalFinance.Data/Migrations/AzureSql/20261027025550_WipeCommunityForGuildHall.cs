using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Data.Migrations.AzureSql
{
    /// <inheritdoc />
    public partial class WipeCommunityForGuildHall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Guild Hall starts from an empty Community (#422, authorized on #396): every row goes, real users'
            // content included, children before parents. A deliberate exception to soft deletes; Down cannot restore it.
            migrationBuilder.Sql(@"
DELETE FROM [CommunityCommentLikes];
DELETE FROM [CommunityStoryLikes];
DELETE FROM [CommunityPollVotes];
DELETE FROM [CommunityComments];
DELETE FROM [CommunityStories];
DELETE FROM [CommunityPollOptions];
DELETE FROM [CommunityPolls];");

            migrationBuilder.DropColumn(
                name: "CommentCount",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "Likes",
                table: "CommunityStories");

            migrationBuilder.DropColumn(
                name: "CaseStudyTitle",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "Likes",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "CommunityComments");

            migrationBuilder.AddColumn<int>(
                name: "StoryId",
                table: "CommunityComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_StoryId",
                table: "CommunityComments",
                column: "StoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityComments_CommunityStories_StoryId",
                table: "CommunityComments",
                column: "StoryId",
                principalTable: "CommunityStories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityComments_CommunityStories_StoryId",
                table: "CommunityComments");

            migrationBuilder.DropIndex(
                name: "IX_CommunityComments_StoryId",
                table: "CommunityComments");

            migrationBuilder.DropColumn(
                name: "StoryId",
                table: "CommunityComments");

            migrationBuilder.AddColumn<int>(
                name: "CommentCount",
                table: "CommunityStories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Likes",
                table: "CommunityStories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CaseStudyTitle",
                table: "CommunityComments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Likes",
                table: "CommunityComments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "CommunityComments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
