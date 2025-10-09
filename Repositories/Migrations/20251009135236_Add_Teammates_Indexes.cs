using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class Add_Teammates_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_University",
                table: "users",
                column: "University");

            migrationBuilder.CreateIndex(
                name: "IX_user_games_Skill",
                table: "user_games",
                column: "Skill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_University",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_games_Skill",
                table: "user_games");
        }
    }
}
