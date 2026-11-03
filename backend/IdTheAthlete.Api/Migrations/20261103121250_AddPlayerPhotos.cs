using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoCredit",
                table: "Players",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoFile",
                table: "Players",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhotoFocusX",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "PhotoFocusY",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<string>(
                name: "PhotoLicense",
                table: "Players",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoCredit",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PhotoFile",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PhotoFocusX",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PhotoFocusY",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PhotoLicense",
                table: "Players");
        }
    }
}
