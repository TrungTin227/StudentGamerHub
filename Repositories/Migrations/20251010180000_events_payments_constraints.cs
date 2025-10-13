using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class events_payments_constraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isNpgsql = migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

            if (isNpgsql)
            {
                migrationBuilder.Sql("ALTER TABLE \"payment_intents\" DROP CONSTRAINT IF EXISTS \"CK_PI_EventTicket_RequiresER\";");
                migrationBuilder.Sql("ALTER TABLE \"payment_intents\" DROP CONSTRAINT IF EXISTS \"CK_PI_NonTicket_NoER\";");
            }
            else
            {
                migrationBuilder.Sql(
                    "IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PI_EventTicket_RequiresER') ALTER TABLE [payment_intents] DROP CONSTRAINT [CK_PI_EventTicket_RequiresER];");
                migrationBuilder.Sql(
                    "IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PI_NonTicket_NoER') ALTER TABLE [payment_intents] DROP CONSTRAINT [CK_PI_NonTicket_NoER];");
            }

            if (isNpgsql)
            {
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_transactions_WalletId\";");
            }
            else
            {
                migrationBuilder.Sql(
                    "IF EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_transactions_WalletId') DROP INDEX [IX_transactions_WalletId] ON [transactions];");
            }

            migrationBuilder.CreateIndex(
                name: "IX_events_StartsAt",
                table: "events",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_EventId_Status",
                table: "event_registrations",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_WalletId",
                table: "transactions",
                column: "WalletId");

            if (isNpgsql)
            {
                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_price_nonneg",
                    table: "events",
                    sql: "\"PriceCents\" >= 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_platform_fee_range",
                    table: "events",
                    sql: "\"PlatformFeeRate\" >= 0 AND \"PlatformFeeRate\" <= 1");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_starts_before_ends",
                    table: "events",
                    sql: "\"EndsAt\" IS NULL OR \"StartsAt\" < \"EndsAt\"");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_status_allowed",
                    table: "events",
                    sql: "\"Status\" IN ('Draft','Open','Closed','Completed','Canceled')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_registration_status_allowed",
                    table: "event_registrations",
                    sql: "\"Status\" IN ('Pending','Confirmed','Canceled','Refunded')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_escrow_status_allowed",
                    table: "escrows",
                    sql: "\"Status\" IN ('Held','Released','Refunded')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_wallet_balance_nonneg",
                    table: "wallets",
                    sql: "\"BalanceCents\" >= 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_amount_positive",
                    table: "transactions",
                    sql: "\"AmountCents\" > 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_direction_allowed",
                    table: "transactions",
                    sql: "\"Direction\" IN ('In','Out')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_method_allowed",
                    table: "transactions",
                    sql: "\"Method\" IN ('Wallet','Gateway','Manual')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_status_allowed",
                    table: "transactions",
                    sql: "\"Status\" IN ('Pending','Succeeded','Failed','Refunded')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_amount_positive",
                    table: "payment_intents",
                    sql: "\"AmountCents\" > 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_purpose_allowed",
                    table: "payment_intents",
                    sql: "\"Purpose\" IN ('TopUp','EventTicket')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_status_allowed",
                    table: "payment_intents",
                    sql: "\"Status\" IN ('RequiresPayment','Succeeded','Canceled','Expired')");
            }
            else
            {
                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_price_nonneg",
                    table: "events",
                    sql: "[PriceCents] >= 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_platform_fee_range",
                    table: "events",
                    sql: "[PlatformFeeRate] >= 0 AND [PlatformFeeRate] <= 1");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_starts_before_ends",
                    table: "events",
                    sql: "[EndsAt] IS NULL OR [StartsAt] < [EndsAt]");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_status_allowed",
                    table: "events",
                    sql: "[Status] IN ('Draft','Open','Closed','Completed','Canceled')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_event_registration_status_allowed",
                    table: "event_registrations",
                    sql: "[Status] IN ('Pending','Confirmed','Canceled','Refunded')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_escrow_status_allowed",
                    table: "escrows",
                    sql: "[Status] IN ('Held','Released','Refunded')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_wallet_balance_nonneg",
                    table: "wallets",
                    sql: "[BalanceCents] >= 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_amount_positive",
                    table: "transactions",
                    sql: "[AmountCents] > 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_direction_allowed",
                    table: "transactions",
                    sql: "[Direction] IN ('In','Out')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_method_allowed",
                    table: "transactions",
                    sql: "[Method] IN ('Wallet','Gateway','Manual')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_transaction_status_allowed",
                    table: "transactions",
                    sql: "[Status] IN ('Pending','Succeeded','Failed','Refunded')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_amount_positive",
                    table: "payment_intents",
                    sql: "[AmountCents] > 0");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_purpose_allowed",
                    table: "payment_intents",
                    sql: "[Purpose] IN ('TopUp','EventTicket')");

                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_status_allowed",
                    table: "payment_intents",
                    sql: "[Status] IN ('RequiresPayment','Succeeded','Canceled','Expired')");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isNpgsql = migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

            migrationBuilder.DropIndex(
                name: "IX_events_StartsAt",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_event_registrations_EventId_Status",
                table: "event_registrations");

            migrationBuilder.DropIndex(
                name: "IX_transactions_WalletId",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_event_price_nonneg",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "chk_event_platform_fee_range",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "chk_event_starts_before_ends",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "chk_event_status_allowed",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "chk_event_registration_status_allowed",
                table: "event_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "chk_escrow_status_allowed",
                table: "escrows");

            migrationBuilder.DropCheckConstraint(
                name: "chk_wallet_balance_nonneg",
                table: "wallets");

            migrationBuilder.DropCheckConstraint(
                name: "chk_transaction_amount_positive",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_transaction_direction_allowed",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_transaction_method_allowed",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_transaction_status_allowed",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_amount_positive",
                table: "payment_intents");

            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_status_allowed",
                table: "payment_intents");

            if (isNpgsql)
            {
                migrationBuilder.AddCheckConstraint(
                    name: "CK_PI_EventTicket_RequiresER",
                    table: "payment_intents",
                    sql: "\"Purpose\" <> 'EventTicket' OR \"EventRegistrationId\" IS NOT NULL");

                migrationBuilder.AddCheckConstraint(
                    name: "CK_PI_NonTicket_NoER",
                    table: "payment_intents",
                    sql: "\"Purpose\" = 'EventTicket' OR \"EventRegistrationId\" IS NULL");
            }
            else
            {
                migrationBuilder.AddCheckConstraint(
                    name: "CK_PI_EventTicket_RequiresER",
                    table: "payment_intents",
                    sql: "[Purpose] <> 'EventTicket' OR [EventRegistrationId] IS NOT NULL");

                migrationBuilder.AddCheckConstraint(
                    name: "CK_PI_NonTicket_NoER",
                    table: "payment_intents",
                    sql: "[Purpose] = 'EventTicket' OR [EventRegistrationId] IS NULL");
            }
        }
    }
}
