using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_DiscoveryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bug_reports_UserId",
                table: "bug_reports");

            migrationBuilder.RenameIndex(
                name: "IX_community_games_GameId",
                table: "community_games",
                newName: "IX_CommunityGame_GameId");

            migrationBuilder.RenameIndex(
                name: "IX_communities_School",
                table: "communities",
                newName: "IX_Community_School");

            migrationBuilder.RenameIndex(
                name: "IX_communities_IsPublic",
                table: "communities",
                newName: "IX_Community_IsPublic");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "bug_reports",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "bug_reports",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "bug_reports",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "bug_reports",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMember_JoinedAt",
                table: "room_members",
                column: "JoinedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMember_Room_JoinedAt",
                table: "room_members",
                columns: new[] { "RoomId", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGame_CommunityId",
                table: "community_games",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Community_CreatedAt",
                table: "communities",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Community_Public_Members",
                table: "communities",
                columns: new[] { "IsPublic", "MembersCount" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Status_CreatedAt",
                table: "bug_reports",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_User_CreatedAt",
                table: "bug_reports",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomMember_JoinedAt",
                table: "room_members");

            migrationBuilder.DropIndex(
                name: "IX_RoomMember_Room_JoinedAt",
                table: "room_members");

            migrationBuilder.DropIndex(
                name: "IX_CommunityGame_CommunityId",
                table: "community_games");

            migrationBuilder.DropIndex(
                name: "IX_Community_CreatedAt",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_Community_Public_Members",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_Status_CreatedAt",
                table: "bug_reports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_User_CreatedAt",
                table: "bug_reports");

            migrationBuilder.RenameIndex(
                name: "IX_CommunityGame_GameId",
                table: "community_games",
                newName: "IX_community_games_GameId");

            migrationBuilder.RenameIndex(
                name: "IX_Community_School",
                table: "communities",
                newName: "IX_communities_School");

            migrationBuilder.RenameIndex(
                name: "IX_Community_IsPublic",
                table: "communities",
                newName: "IX_communities_IsPublic");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "bug_reports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "bug_reports",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "bug_reports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "bug_reports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_bug_reports_UserId",
                table: "bug_reports",
                column: "UserId");
        }
    }
}
