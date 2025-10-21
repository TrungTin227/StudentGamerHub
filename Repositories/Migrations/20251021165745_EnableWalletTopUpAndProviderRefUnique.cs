using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class EnableWalletTopUpAndProviderRefUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isNpgsql = migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

            // 1. Drop existing purpose constraint
            if (isNpgsql)
            {
                migrationBuilder.Sql("ALTER TABLE \"payment_intents\" DROP CONSTRAINT IF EXISTS \"chk_payment_intent_purpose_allowed\";");
            }
            else
            {
                migrationBuilder.Sql(
                    "IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_payment_intent_purpose_allowed') " +
                    "ALTER TABLE [payment_intents] DROP CONSTRAINT [chk_payment_intent_purpose_allowed];");
            }

            // 2. Add updated constraint with WalletTopUp
            if (isNpgsql)
            {
                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_purpose_allowed",
                    table: "payment_intents",
                    sql: "\"Purpose\" IN ('TopUp','EventTicket','WalletTopUp')");
            }
            else
            {
                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_purpose_allowed",
                    table: "payment_intents",
                    sql: "[Purpose] IN ('TopUp','EventTicket','WalletTopUp')");
            }

            // 3. Add unique constraint on (Provider, ProviderRef) for idempotency
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Provider_ProviderRef",
                table: "transactions",
                columns: new[] { "Provider", "ProviderRef" },
                unique: true,
                filter: isNpgsql
                    ? "\"Provider\" IS NOT NULL AND \"ProviderRef\" IS NOT NULL"
                    : "[Provider] IS NOT NULL AND [ProviderRef] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isNpgsql = migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

            // 1. Drop unique index
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Provider_ProviderRef",
                table: "transactions");

            // 2. Drop updated constraint
            migrationBuilder.DropCheckConstraint(
                name: "chk_payment_intent_purpose_allowed",
                table: "payment_intents");

            // 3. Restore original constraint without WalletTopUp
            if (isNpgsql)
            {
                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_purpose_allowed",
                    table: "payment_intents",
                    sql: "\"Purpose\" IN ('TopUp','EventTicket')");
            }
            else
            {
                migrationBuilder.AddCheckConstraint(
                    name: "chk_payment_intent_purpose_allowed",
                    table: "payment_intents",
                    sql: "[Purpose] IN ('TopUp','EventTicket')");
            }
        }
    }
}
