using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSportFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Family",
                table: "Sports",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Existing sports get their family from their slug prefix. Any
            // sport left with an empty family has no rules, and the app's
            // startup check (Program.cs) refuses to start until it's fixed,
            // rather than scoring it with the wrong rules.
            migrationBuilder.Sql("""UPDATE "Sports" SET "Family" = 'tennis' WHERE "Slug" LIKE 'tennis-%';""");
            migrationBuilder.Sql("""UPDATE "Sports" SET "Family" = 'cricket' WHERE "Slug" LIKE 'cricket-%';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Family",
                table: "Sports");
        }
    }
}
