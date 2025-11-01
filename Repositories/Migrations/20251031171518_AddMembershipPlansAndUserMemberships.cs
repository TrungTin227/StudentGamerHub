using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipPlansAndUserMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipPlanId",
                table: "payment_intents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "membership_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MonthlyEventLimit = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_membership_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RemainingEventQuota = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_user_memberships", x => x.Id);
                    table.CheckConstraint("chk_user_membership_quota_nonneg", "\"RemainingEventQuota\" >= 0");
                    table.ForeignKey(
                        name: "FK_user_memberships_membership_plans_MembershipPlanId",
                        column: x => x.MembershipPlanId,
                        principalTable: "membership_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "membership_plans",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Description", "DurationMonths", "IsActive", "IsDeleted", "MonthlyEventLimit", "Name", "Price", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("088f4b43-93fc-4c92-8b24-20f84a3d9210"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Create up to 3 events per month.", 1, true, false, 3, "Basic", 99000m, null, null },
                    { new Guid("31b8dcfb-2e5d-4b84-aefb-cbc5a8db7c39"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Create up to 10 events per month.", 1, true, false, 10, "Pro", 199000m, null, null },
                    { new Guid("517899aa-03ec-4907-bd1b-b3be526a441a"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Unlimited event creation per month.", 1, true, false, 0, "Ultimate", 499000m, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_MembershipPlanId",
                table: "payment_intents",
                column: "MembershipPlanId");

            migrationBuilder.AddCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents",
                sql: "\"Purpose\" IN ('TopUp','EventTicket','WalletTopUp','Membership')");

            migrationBuilder.CreateIndex(
                name: "IX_membership_plans_IsActive",
                table: "membership_plans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_membership_plans_Name",
                table: "membership_plans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_memberships_EndDate",
                table: "user_memberships",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_user_memberships_MembershipPlanId",
                table: "user_memberships",
                column: "MembershipPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_user_memberships_UserId",
                table: "user_memberships",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_membership_plans_MembershipPlanId",
                table: "payment_intents",
                column: "MembershipPlanId",
                principalTable: "membership_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_membership_plans_MembershipPlanId",
                table: "payment_intents");

            migrationBuilder.DropTable(
                name: "user_memberships");

            migrationBuilder.DropTable(
                name: "membership_plans");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_MembershipPlanId",
                table: "payment_intents");

            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "MembershipPlanId",
                table: "payment_intents");

            migrationBuilder.AddCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents",
                sql: "\"Purpose\" IN ('TopUp','EventTicket','WalletTopUp')");
        }
    }
}
