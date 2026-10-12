using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    // IsManuallyEdited only existed so seed-file re-runs wouldn't overwrite
    // values a curator had corrected in the admin tool. With the seed files
    // retired, nothing writes over those values any more, so it's dropped.
    /// <inheritdoc />
    public partial class RemoveIsManuallyEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "PlayerAttributeValues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the column, but not which values had been edited:
            // every row comes back as false.
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "PlayerAttributeValues",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
