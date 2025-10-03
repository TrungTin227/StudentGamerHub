using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class StudentGamerHub3NF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_escrows_users_OwnerUserId",
                table: "escrows");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_events_EventId",
                table: "payment_intents");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_users_UserId",
                table: "payment_intents");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_users_UserId",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_wallets_WalletId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_UserId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_UserId_CreatedAtUtc",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_WalletId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_EventId",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_UserId_Status",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_escrows_OwnerUserId",
                table: "escrows");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "escrows");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "payment_intents",
                newName: "EventRegistrationId");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_WalletId_CreatedAtUtc",
                table: "transactions",
                columns: new[] { "WalletId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_EventRegistrationId",
                table: "payment_intents",
                column: "EventRegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_UserId",
                table: "payment_intents",
                column: "UserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PI_EventTicket_RequiresER",
                table: "payment_intents",
                sql: "\"Purpose\" <> 'EventTicket' OR \"EventRegistrationId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PI_NonTicket_NoER",
                table: "payment_intents",
                sql: "\"Purpose\" = 'EventTicket' OR \"EventRegistrationId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_escrow_amount_nonneg",
                table: "escrows",
                sql: "\"AmountHoldCents\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_event_registrations_EventRegistrationId",
                table: "payment_intents",
                column: "EventRegistrationId",
                principalTable: "event_registrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_users_UserId",
                table: "payment_intents",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_wallets_WalletId",
                table: "transactions",
                column: "WalletId",
                principalTable: "wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_event_registrations_EventRegistrationId",
                table: "payment_intents");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_users_UserId",
                table: "payment_intents");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_wallets_WalletId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_WalletId_CreatedAtUtc",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_EventRegistrationId",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_UserId",
                table: "payment_intents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PI_EventTicket_RequiresER",
                table: "payment_intents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PI_NonTicket_NoER",
                table: "payment_intents");

            migrationBuilder.DropCheckConstraint(
                name: "chk_escrow_amount_nonneg",
                table: "escrows");

            migrationBuilder.RenameColumn(
                name: "EventRegistrationId",
                table: "payment_intents",
                newName: "EventId");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                table: "transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "escrows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_transactions_UserId",
                table: "transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_UserId_CreatedAtUtc",
                table: "transactions",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_WalletId",
                table: "transactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_EventId",
                table: "payment_intents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_UserId_Status",
                table: "payment_intents",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_escrows_OwnerUserId",
                table: "escrows",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_escrows_users_OwnerUserId",
                table: "escrows",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_events_EventId",
                table: "payment_intents",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_users_UserId",
                table: "payment_intents",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_users_UserId",
                table: "transactions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_wallets_WalletId",
                table: "transactions",
                column: "WalletId",
                principalTable: "wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
