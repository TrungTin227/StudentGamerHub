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
          migrationBuilder.AddColumn<long>(
         name: "OrderCode",
      table: "payment_intents",
     type: "bigint",
                nullable: true);

         migrationBuilder.CreateIndex(
           name: "IX_payment_intents_OrderCode",
           table: "payment_intents",
    column: "OrderCode",
      unique: true,
     filter: "\"OrderCode\" IS NOT NULL");
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
