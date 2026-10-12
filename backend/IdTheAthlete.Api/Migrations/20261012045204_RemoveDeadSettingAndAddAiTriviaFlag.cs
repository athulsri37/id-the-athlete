using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    // Data-only migration (no schema change). Settings data normally comes from
    // SeedTool, but this cleanup has to reach every existing database,
    // production included, without depending on anyone running SeedTool there.
    // Migrations run automatically on startup, so this is the one place that
    // guarantees it. SeedData/00-app-settings.sql was updated to match, so a
    // database built from scratch ends up in the same state.
    /// <inheritdoc />
    public partial class RemoveDeadSettingAndAddAiTriviaFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added in #46 as a planned switch for Cricket's Role column but
            // never read by any code; it only showed up, inert, in the admin
            // tool's settings list.
            migrationBuilder.Sql("""DELETE FROM "AppSettings" WHERE "Key" = 'CricketRoleClueEnabled';""");

            // AiTriviaService has always treated a missing row as off, but the
            // admin tool can only edit settings that exist, so trivia could not
            // be switched on from it. Insert the row as off (today's behaviour),
            // leaving any value an operator already set untouched.
            migrationBuilder.Sql("""
                INSERT INTO "AppSettings" ("Key", "Value")
                VALUES ('AiTriviaEnabled', 'false')
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only remove the trivia row if it still holds the default this
            // migration inserted; a value an operator has since changed stays.
            migrationBuilder.Sql("""DELETE FROM "AppSettings" WHERE "Key" = 'AiTriviaEnabled' AND "Value" = 'false';""");

            migrationBuilder.Sql("""
                INSERT INTO "AppSettings" ("Key", "Value")
                VALUES ('CricketRoleClueEnabled', 'true')
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }
    }
}
