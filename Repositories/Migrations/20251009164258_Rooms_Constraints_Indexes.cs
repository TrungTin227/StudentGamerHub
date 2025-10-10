using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class Rooms_Constraints_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_rooms_Capacity",
                table: "rooms",
                column: "Capacity");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_ClubId",
                table: "rooms",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_JoinPolicy",
                table: "rooms",
                column: "JoinPolicy");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_MembersCount",
                table: "rooms",
                column: "MembersCount");

            migrationBuilder.AddCheckConstraint(
                name: "chk_room_password_required",
                table: "rooms",
                sql: "(\"JoinPolicy\" <> '2') OR (\"JoinPasswordHash\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_room_members_Status",
                table: "room_members",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_communities_IsPublic",
                table: "communities",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_communities_MembersCount",
                table: "communities",
                column: "MembersCount");

            migrationBuilder.CreateIndex(
                name: "IX_communities_School",
                table: "communities",
                column: "School");

            migrationBuilder.CreateIndex(
                name: "IX_clubs_CommunityId",
                table: "clubs",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_clubs_MembersCount",
                table: "clubs",
                column: "MembersCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rooms_Capacity",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_ClubId",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_JoinPolicy",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_MembersCount",
                table: "rooms");

            migrationBuilder.DropCheckConstraint(
                name: "chk_room_password_required",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_room_members_Status",
                table: "room_members");

            migrationBuilder.DropIndex(
                name: "IX_communities_IsPublic",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_communities_MembersCount",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_communities_School",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_clubs_CommunityId",
                table: "clubs");

            migrationBuilder.DropIndex(
                name: "IX_clubs_MembersCount",
                table: "clubs");
        }
    }
}
