using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSportShortName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Sports",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Existing tours get the labels the tour-select screen showed when
            // they were hard-coded in the frontend, so it looks the same. The
            // seed files set the same values; any other tour stays empty and
            // the frontend falls back to its full Name.
            migrationBuilder.Sql("""
                UPDATE "Sports" SET "ShortName" = CASE "Slug"
                    WHEN 'tennis-men' THEN 'Men''s'
                    WHEN 'tennis-women' THEN 'Women''s'
                    WHEN 'cricket-men-international' THEN 'Men''s International'
                    WHEN 'cricket-women-international' THEN 'Women''s International'
                    ELSE "ShortName"
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Sports");
        }
    }
}
