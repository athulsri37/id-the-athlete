using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisGuessr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDifficultyOverrideRemoveEraGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EraGroup",
                table: "Players");

            migrationBuilder.AddColumn<string>(
                name: "DifficultyOverride",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverridden",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DifficultyOverride",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsOverridden",
                table: "Players");

            migrationBuilder.AddColumn<string>(
                name: "EraGroup",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
