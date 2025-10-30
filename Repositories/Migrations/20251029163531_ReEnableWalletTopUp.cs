using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class ReEnableWalletTopUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            migrationBuilder.AddCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents",
                sql: "\"Purpose\" IN ('TopUp','EventTicket','WalletTopUp')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            migrationBuilder.AddCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents",
                sql: "\"Purpose\" IN ('TopUp','EventTicket')");
        }
    }
}
