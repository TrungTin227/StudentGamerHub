using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityClubMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_room_password_required",
                table: "rooms");

            migrationBuilder.RenameIndex(
                name: "IX_room_members_UserId",
                table: "room_members",
                newName: "IX_RoomMembers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_room_members_RoomId_Status",
                table: "room_members",
                newName: "IX_RoomMember_RoomId_Status");

            migrationBuilder.CreateTable(
                name: "club_members",
                columns: table => new
                {
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_members", x => new { x.ClubId, x.UserId });
                    table.ForeignKey(
                        name: "FK_club_members_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_members",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_members", x => new { x.CommunityId, x.UserId });
                    table.ForeignKey(
                        name: "FK_community_members_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_community_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "chk_room_password_required",
                table: "rooms",
                sql: "(\"JoinPolicy\" <> 'RequiresPassword') OR (\"JoinPasswordHash\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_club_members_ClubId_UserId",
                table: "club_members",
                columns: new[] { "ClubId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_UserId",
                table: "club_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_members_CommunityId_UserId",
                table: "community_members",
                columns: new[] { "CommunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMembers_UserId",
                table: "community_members",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_members");

            migrationBuilder.DropTable(
                name: "community_members");

            migrationBuilder.DropCheckConstraint(
                name: "chk_room_password_required",
                table: "rooms");

            migrationBuilder.RenameIndex(
                name: "IX_RoomMembers_UserId",
                table: "room_members",
                newName: "IX_room_members_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_RoomMember_RoomId_Status",
                table: "room_members",
                newName: "IX_room_members_RoomId_Status");

            migrationBuilder.AddCheckConstraint(
                name: "chk_room_password_required",
                table: "rooms",
                sql: "(\"JoinPolicy\" <> '2') OR (\"JoinPasswordHash\" IS NOT NULL)");
        }
    }
}
