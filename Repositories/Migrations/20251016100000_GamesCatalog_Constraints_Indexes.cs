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
            // Convert existing skill values to numeric representation before changing column type
            if (migrationBuilder.ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.Sql("""
                    UPDATE "user_games"
                    SET "Skill" = CASE
                        WHEN "Skill" IN ('Casual', '0') THEN '0'
                        WHEN "Skill" IN ('Intermediate', '1') THEN '1'
                        WHEN "Skill" IN ('Competitive', '2') THEN '2'
                        ELSE NULL
                    END;
                """);
            }
            else
            {
                migrationBuilder.Sql("""
                    UPDATE [user_games]
                    SET [Skill] = CASE
                        WHEN [Skill] = 'Casual' THEN '0'
                        WHEN [Skill] = 'Intermediate' THEN '1'
                        WHEN [Skill] = 'Competitive' THEN '2'
                        ELSE [Skill]
                    END;
                """);
            }

            // Adjust Game.Name column length/type per provider
            if (migrationBuilder.ActiveProvider.Contains("Npgsql"))
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

            // Drop legacy indexes on user_games
            migrationBuilder.DropIndex(
                name: "IX_user_games_GameId",
                table: "user_games");

            migrationBuilder.DropIndex(
                name: "IX_user_games_Skill",
                table: "user_games");

            // Change skill column to integer
            if (migrationBuilder.ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.AlterColumn<int>(
                    name: "Skill",
                    table: "user_games",
                    type: "integer",
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "text",
                    oldNullable: true);
            }
            else
            {
                migrationBuilder.AlterColumn<int>(
                    name: "Skill",
                    table: "user_games",
                    type: "int",
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "nvarchar(max)",
                    oldNullable: true);
            }

            // Drop existing foreign keys to replace with Restrict behavior
            migrationBuilder.DropForeignKey(
                name: "FK_user_games_games_GameId",
                table: "user_games");

            migrationBuilder.DropForeignKey(
                name: "FK_user_games_users_UserId",
                table: "user_games");

            // Create updated indexes with explicit names
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

            // Provider-specific unique index on Name (case-insensitive)
            if (migrationBuilder.ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.Sql("""
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Games_Name_Lower_UQ"
                    ON "games" (LOWER("Name"))
                    WHERE "IsDeleted" = false;
                """);

                migrationBuilder.Sql("""
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_constraint WHERE conname = 'CK_UserGames_Skill_Range'
                        ) THEN
                            ALTER TABLE "user_games"
                            ADD CONSTRAINT "CK_UserGames_Skill_Range"
                            CHECK ("Skill" IS NULL OR "Skill" BETWEEN 0 AND 2);
                        END IF;
                    END$$;
                """);
            }
            else
            {
                migrationBuilder.CreateIndex(
                    name: "IX_Games_Name_CI",
                    table: "games",
                    column: "Name",
                    unique: true,
                    filter: "[IsDeleted] = 0");

                migrationBuilder.AddCheckConstraint(
                    name: "CK_UserGames_Skill_Range",
                    table: "user_games",
                    sql: "[Skill] IS NULL OR [Skill] BETWEEN 0 AND 2");
            }

            // Re-add foreign keys with Restrict delete behavior
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
            // Remove foreign keys with Restrict behavior
            migrationBuilder.DropForeignKey(
                name: "FK_user_games_games_GameId",
                table: "user_games");

            migrationBuilder.DropForeignKey(
                name: "FK_user_games_users_UserId",
                table: "user_games");

            // Drop new indexes
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

            if (migrationBuilder.ActiveProvider.Contains("Npgsql"))
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

            // Convert skill values back to string representation before altering column type
            if (migrationBuilder.ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.Sql("""
                    UPDATE "user_games"
                    SET "Skill" = CASE
                        WHEN "Skill" = 0 THEN 'Casual'
                        WHEN "Skill" = 1 THEN 'Intermediate'
                        WHEN "Skill" = 2 THEN 'Competitive'
                        ELSE NULL
                    END;
                """);

                migrationBuilder.AlterColumn<string>(
                    name: "Skill",
                    table: "user_games",
                    type: "text",
                    nullable: true,
                    oldClrType: typeof(int),
                    oldType: "integer",
                    oldNullable: true);

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
                migrationBuilder.Sql("""
                    UPDATE [user_games]
                    SET [Skill] = CASE
                        WHEN [Skill] = 0 THEN 'Casual'
                        WHEN [Skill] = 1 THEN 'Intermediate'
                        WHEN [Skill] = 2 THEN 'Competitive'
                        ELSE NULL
                    END;
                """);

                migrationBuilder.AlterColumn<string>(
                    name: "Skill",
                    table: "user_games",
                    type: "nvarchar(max)",
                    nullable: true,
                    oldClrType: typeof(int),
                    oldType: "int",
                    oldNullable: true);

                migrationBuilder.AlterColumn<string>(
                    name: "Name",
                    table: "games",
                    type: "nvarchar(max)",
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "nvarchar(128)",
                    oldMaxLength: 128);
            }

            // Recreate previous indexes
            migrationBuilder.CreateIndex(
                name: "IX_user_games_GameId",
                table: "user_games",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_user_games_Skill",
                table: "user_games",
                column: "Skill");

            // Restore cascading foreign keys
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
