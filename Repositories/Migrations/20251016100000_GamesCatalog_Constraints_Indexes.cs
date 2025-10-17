using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations
{
    /// <inheritdoc />
    public partial class GamesCatalog_Constraints_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isNpgsql = migrationBuilder.ActiveProvider.Contains("Npgsql");

            // ====== 1. Adjust Game.Name column length/type per provider ======
            if (isNpgsql)
            {
                migrationBuilder.AlterColumn<string>(
                    name: "Name",
                    table: "games",
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "text");
            }
            else
            {
                migrationBuilder.AlterColumn<string>(
                    name: "Name",
                    table: "games",
                    type: "nvarchar(128)",
                    maxLength: 128,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "nvarchar(max)");
            }

            // ====== 2. Drop legacy indexes on user_games ======
            migrationBuilder.DropIndex(
                name: "IX_user_games_GameId",
                table: "user_games");

            migrationBuilder.DropIndex(
                name: "IX_user_games_Skill",
                table: "user_games");

            // ====== 3. Drop existing foreign keys to replace with Restrict behavior ======
            migrationBuilder.DropForeignKey(
                name: "FK_user_games_games_GameId",
                table: "user_games");

            migrationBuilder.DropForeignKey(
                name: "FK_user_games_users_UserId",
                table: "user_games");

            // ====== 4. Create updated indexes with explicit names ======
            migrationBuilder.CreateIndex(
                name: "IX_UserGames_GameId",
                table: "user_games",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGames_Skill",
                table: "user_games",
                column: "Skill");

            migrationBuilder.CreateIndex(
                name: "IX_UserGames_UserId",
                table: "user_games",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_CreatedAtUtc",
                table: "games",
                column: "CreatedAtUtc");

            // ====== 5. Provider-specific unique index on Name (case-insensitive) ======
            if (isNpgsql)
            {
                // PostgreSQL: Create unique index on LOWER(Name) for case-insensitive uniqueness
                migrationBuilder.Sql("""
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Games_Name_Lower_UQ"
                    ON "games" (LOWER("Name"))
                    WHERE "IsDeleted" = false;
                """);
            }
            else
            {
                // SQL Server: Use built-in case-insensitive collation
                migrationBuilder.CreateIndex(
                    name: "IX_Games_Name_CI",
                    table: "games",
                    column: "Name",
                    unique: true,
                    filter: "[IsDeleted] = 0");
            }

            // ====== 6. Add check constraint for Skill (string enum values) ======
            if (isNpgsql)
            {
                migrationBuilder.Sql("""
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_constraint WHERE conname = 'CK_UserGames_Skill_Range'
                        ) THEN
                            ALTER TABLE "user_games"
                            ADD CONSTRAINT "CK_UserGames_Skill_Range"
                            CHECK ("Skill" IS NULL OR "Skill" IN ('Casual','Intermediate','Competitive'));
                        END IF;
                    END$$;
                """);
            }
            else
            {
                migrationBuilder.AddCheckConstraint(
                    name: "CK_UserGames_Skill_Range",
                    table: "user_games",
                    sql: "[Skill] IS NULL OR [Skill] IN ('Casual','Intermediate','Competitive')");
            }

            // ====== 7. Re-add foreign keys with Restrict delete behavior ======
            migrationBuilder.AddForeignKey(
                name: "FK_user_games_games_GameId",
                table: "user_games",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_games_users_UserId",
                table: "user_games",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isNpgsql = migrationBuilder.ActiveProvider.Contains("Npgsql");

            // ====== 1. Remove foreign keys with Restrict behavior ======
            migrationBuilder.DropForeignKey(
                name: "FK_user_games_games_GameId",
                table: "user_games");

            migrationBuilder.DropForeignKey(
                name: "FK_user_games_users_UserId",
                table: "user_games");

            // ====== 2. Drop new indexes ======
            migrationBuilder.DropIndex(
                name: "IX_UserGames_GameId",
                table: "user_games");

            migrationBuilder.DropIndex(
                name: "IX_UserGames_Skill",
                table: "user_games");

            migrationBuilder.DropIndex(
                name: "IX_UserGames_UserId",
                table: "user_games");

            migrationBuilder.DropIndex(
                name: "IX_Games_CreatedAtUtc",
                table: "games");

            // ====== 3. Drop unique index and check constraint ======
            if (isNpgsql)
            {
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Games_Name_Lower_UQ\";");
                migrationBuilder.Sql("""
                    ALTER TABLE "user_games"
                    DROP CONSTRAINT IF EXISTS "CK_UserGames_Skill_Range";
                """);
            }
            else
            {
                migrationBuilder.DropIndex(
                    name: "IX_Games_Name_CI",
                    table: "games");

                migrationBuilder.DropCheckConstraint(
                    name: "CK_UserGames_Skill_Range",
                    table: "user_games");
            }

            // ====== 4. Restore Game.Name column to unlimited length ======
            if (isNpgsql)
            {
                migrationBuilder.AlterColumn<string>(
                    name: "Name",
                    table: "games",
                    type: "text",
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "character varying(128)",
                    oldMaxLength: 128);
            }
            else
            {
                migrationBuilder.AlterColumn<string>(
                    name: "Name",
                    table: "games",
                    type: "nvarchar(max)",
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "nvarchar(128)",
                    oldMaxLength: 128);
            }

            // ====== 5. Recreate previous indexes ======
            migrationBuilder.CreateIndex(
                name: "IX_user_games_GameId",
                table: "user_games",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_user_games_Skill",
                table: "user_games",
                column: "Skill");

            // ====== 6. Restore cascading foreign keys ======
            migrationBuilder.AddForeignKey(
                name: "FK_user_games_games_GameId",
                table: "user_games",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_games_users_UserId",
                table: "user_games",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
