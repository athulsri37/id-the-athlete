using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddYearClosenessSettings : Migration
    {
        /// <inheritdoc />
        // How close a year clue must be to count as close: Cricket's Debut
        // Year had no closeness before, and Tennis's Turned Pro was a fixed 3
        // in TennisRules. Both are now settings, defaulting to 3 years. Only
        // fills gaps, so a value already changed in the Control Room stays.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "AppSettings" ("Key", "Value")
                VALUES
                    ('CricketDebutYearClosenessYears', '3'),
                    ('TennisTurnedProClosenessYears', '3')
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AppSettings"
                WHERE "Key" IN ('CricketDebutYearClosenessYears', 'TennisTurnedProClosenessYears');
                """);
        }
    }
}
