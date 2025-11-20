using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_registrations_UserId",
                table: "event_registrations");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_Direction",
                table: "transactions",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_Status",
                table: "transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_Status_Direction_CreatedAtUtc",
                table: "transactions",
                columns: new[] { "Status", "Direction", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_Status_CreatedAtUtc",
                table: "payment_intents",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_Status_Purpose",
                table: "payment_intents",
                columns: new[] { "Status", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_UserId_Status",
                table: "event_registrations",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_Direction",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_Status",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_Status_Direction_CreatedAtUtc",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_Status_CreatedAtUtc",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_Status_Purpose",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_event_registrations_UserId_Status",
                table: "event_registrations");

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_UserId",
                table: "event_registrations",
                column: "UserId");
        }
    }
}
