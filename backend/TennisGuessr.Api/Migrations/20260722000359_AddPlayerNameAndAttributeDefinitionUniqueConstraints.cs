using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisGuessr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerNameAndAttributeDefinitionUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttributeDefinitions_SportId",
                table: "AttributeDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_SportId_Key",
                table: "AttributeDefinitions",
                columns: new[] { "SportId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_Name",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_AttributeDefinitions_SportId_Key",
                table: "AttributeDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_SportId",
                table: "AttributeDefinitions",
                column: "SportId");
        }
    }
}
