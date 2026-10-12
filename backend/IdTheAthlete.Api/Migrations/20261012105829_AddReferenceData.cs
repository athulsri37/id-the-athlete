using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    // The reference data the game needs before it has any players: each
    // sport, its clue columns, and the default settings. These used to come
    // from SeedTool's 00-*.sql files; with the database now the source of
    // truth and SeedTool retired, they live here so any new database gets
    // them automatically on startup.
    //
    // Every insert skips rows that already exist (ON CONFLICT DO NOTHING), so
    // on an existing database -- yours, or production copied from it -- this
    // changes nothing, including values a curator has since edited.
    //
    // A new sport adds its own migration like this one, in the same PR as its
    // ISportRules class and its frontend display entry.
    /// <inheritdoc />
    public partial class AddReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Sports" ("Name", "Slug", "Family", "ShortName")
                VALUES
                    ('Men''s Tennis', 'tennis-men', 'tennis', 'Men''s'),
                    ('Women''s Tennis', 'tennis-women', 'tennis', 'Women''s'),
                    ('Men''s International Cricket', 'cricket-men-international', 'cricket', 'Men''s International'),
                    ('Women''s International Cricket', 'cricket-women-international', 'cricket', 'Women''s International')
                ON CONFLICT ("Slug") DO NOTHING;
                """);

            // Type: 0 = Categorical, 1 = Numeric (AttributeType).
            migrationBuilder.Sql("""
                INSERT INTO "AttributeDefinitions" ("SportId", "Key", "Label", "Type", "DisplayOrder")
                SELECT s."Id", v."Key", v."Label", v."Type", v."DisplayOrder"
                FROM "Sports" s
                JOIN (VALUES
                    ('tennis', 'active_status', 'Status', 0, 0),
                    ('tennis', 'plays', 'Plays', 0, 1),
                    ('tennis', 'backhand', 'Backhand', 0, 2),
                    ('tennis', 'country', 'Country', 0, 3),
                    ('tennis', 'grand_slam_titles', 'Grand Slams', 1, 4),
                    ('tennis', 'career_high_ranking', 'Career-High Rank', 1, 5),
                    ('tennis', 'turned_pro_year', 'Turned Pro', 1, 6),
                    ('tennis', 'career_titles', 'Career Titles', 1, 7),
                    ('cricket', 'active_status', 'Status', 0, 0),
                    ('cricket', 'country', 'Country', 0, 1),
                    ('cricket', 'batting_hand', 'Batting Hand', 0, 2),
                    ('cricket', 'bowling_style', 'Bowling Style', 0, 3),
                    ('cricket', 'role', 'Role', 0, 4),
                    ('cricket', 'combined_matches', 'Matches', 1, 5),
                    ('cricket', 'combined_runs', 'Runs', 1, 6),
                    ('cricket', 'combined_wickets', 'Wickets', 1, 7),
                    ('cricket', 'debut_year', 'Debut Year', 1, 8)
                ) AS v("Family", "Key", "Label", "Type", "DisplayOrder") ON v."Family" = s."Family"
                WHERE s."Slug" IN ('tennis-men', 'tennis-women', 'cricket-men-international', 'cricket-women-international')
                ON CONFLICT ("SportId", "Key") DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "AppSettings" ("Key", "Value")
                VALUES
                    ('AiTriviaEnabled', 'false'),
                    ('CountryClosenessEnabled', 'true'),
                    ('CricketRoleClosenessEnabled', 'true'),
                    ('CricketBowlingStyleClosenessEnabled', 'true'),
                    ('CricketMatchesClosenessPercent', '15'),
                    ('CricketMatchesClosenessFloor', '20'),
                    ('CricketRunsClosenessPercent', '15'),
                    ('CricketRunsClosenessFloor', '500'),
                    ('CricketWicketsClosenessPercent', '15'),
                    ('CricketWicketsClosenessFloor', '15')
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately does nothing. Up only fills gaps, so it can't tell
            // rows it inserted from rows that were already there, and deleting
            // a sport or its columns would take every player's data with them.
        }
    }
}
