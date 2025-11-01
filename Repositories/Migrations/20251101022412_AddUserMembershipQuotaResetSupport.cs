using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMembershipQuotaResetSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastResetAtUtc",
                table: "user_memberships",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "membership_plans",
                keyColumn: "Id",
                keyValue: new Guid("517899aa-03ec-4907-bd1b-b3be526a441a"),
                column: "MonthlyEventLimit",
                value: -1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastResetAtUtc",
                table: "user_memberships");

            migrationBuilder.UpdateData(
                table: "membership_plans",
                keyColumn: "Id",
                keyValue: new Guid("517899aa-03ec-4907-bd1b-b3be526a441a"),
                column: "MonthlyEventLimit",
                value: 0);
        }
    }
}
