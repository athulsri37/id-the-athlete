using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSportUrlName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UrlName",
                table: "Sports",
                type: "text",
                nullable: false,
                defaultValue: "");

            // The four tours' web addresses: /tennis/men, /tennis/women,
            // /cricket/men, /cricket/women. A tour not listed stays empty and
            // the frontend uses its slug.
            migrationBuilder.Sql("""
                UPDATE "Sports" SET "UrlName" = CASE "Slug"
                    WHEN 'tennis-men' THEN 'men'
                    WHEN 'tennis-women' THEN 'women'
                    WHEN 'cricket-men-international' THEN 'men'
                    WHEN 'cricket-women-international' THEN 'women'
                    ELSE "UrlName"
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Family_UrlName",
                table: "Sports",
                columns: new[] { "Family", "UrlName" },
                unique: true,
                filter: "\"UrlName\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sports_Family_UrlName",
                table: "Sports");

            migrationBuilder.DropColumn(
                name: "UrlName",
                table: "Sports");
        }
    }
}
