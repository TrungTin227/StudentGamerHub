using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCodeToPaymentIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
// Check if column exists before adding it
     migrationBuilder.Sql(@"
       DO $$ 
        BEGIN 
               IF NOT EXISTS (
              SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payment_intents' AND column_name = 'OrderCode'
    ) THEN
   ALTER TABLE payment_intents ADD COLUMN ""OrderCode"" bigint NULL;
    END IF;
            END $$;
     ");

            // Check if index exists before creating it
 migrationBuilder.Sql(@"
    DO $$ 
         BEGIN 
  IF NOT EXISTS (
                   SELECT 1 FROM pg_indexes 
            WHERE tablename = 'payment_intents' AND indexname = 'IX_payment_intents_OrderCode'
           ) THEN
   CREATE UNIQUE INDEX ""IX_payment_intents_OrderCode"" 
        ON payment_intents (""OrderCode"") 
           WHERE ""OrderCode"" IS NOT NULL;
   END IF;
 END $$;
            ");
        }

 /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.DropIndex(
                name: "IX_payment_intents_OrderCode",
                table: "payment_intents");

       migrationBuilder.DropColumn(
    name: "OrderCode",
         table: "payment_intents");
        }
    }
}
