using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveThemeSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The theme has always defaulted to retro when this row is missing
            // (SettingsService.GetThemeAsync), but the admin tool can only edit
            // settings that exist, so switching themes needed SQL. Insert the
            // row as retro (today's behaviour), leaving any theme an operator
            // already set untouched.
            migrationBuilder.Sql("""
                INSERT INTO "AppSettings" ("Key", "Value")
                VALUES ('ActiveTheme', 'retro')
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only remove the row if it still holds the default this migration
            // inserted; a theme an operator has since chosen stays.
            migrationBuilder.Sql("""DELETE FROM "AppSettings" WHERE "Key" = 'ActiveTheme' AND "Value" = 'retro';""");
        }
    }
}
