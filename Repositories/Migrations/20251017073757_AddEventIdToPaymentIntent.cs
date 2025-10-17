using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddEventIdToPaymentIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "payment_intents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_EventId",
                table: "payment_intents",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_events_EventId",
                table: "payment_intents",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_events_EventId",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_EventId",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "payment_intents");
        }
    }
}
