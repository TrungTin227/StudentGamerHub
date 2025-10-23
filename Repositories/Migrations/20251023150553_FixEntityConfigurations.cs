using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class FixEntityConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_community_members_communities_CommunityId",
                table: "community_members");

            migrationBuilder.DropForeignKey(
                name: "FK_community_members_users_UserId",
                table: "community_members");

            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            // Update existing 'WalletTopUp' values to 'TopUp' before applying new constraint
            migrationBuilder.Sql(@"
                UPDATE payment_intents 
                SET ""Purpose"" = 'TopUp' 
                WHERE ""Purpose"" = 'WalletTopUp';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "community_members",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "club_members",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents",
                sql: "\"Purpose\" IN ('TopUp','EventTicket')");

            migrationBuilder.AddForeignKey(
                name: "FK_community_members_communities_CommunityId",
                table: "community_members",
                column: "CommunityId",
                principalTable: "communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_community_members_users_UserId",
                table: "community_members",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_community_members_communities_CommunityId",
                table: "community_members");

            migrationBuilder.DropForeignKey(
                name: "FK_community_members_users_UserId",
                table: "community_members");

            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "community_members",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "club_members",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents",
                sql: "\"Purpose\" IN ('TopUp','EventTicket','WalletTopUp')");

            migrationBuilder.AddForeignKey(
                name: "FK_community_members_communities_CommunityId",
                table: "community_members",
                column: "CommunityId",
                principalTable: "communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_community_members_users_UserId",
                table: "community_members",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
