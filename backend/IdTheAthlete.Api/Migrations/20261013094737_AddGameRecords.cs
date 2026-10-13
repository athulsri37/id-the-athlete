using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdTheAthlete.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGameRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SportId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PuzzleDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPastChallenge = table.Column<bool>(type: "boolean", nullable: false),
                    MysteryPlayerId = table.Column<int>(type: "integer", nullable: true),
                    GuessCount = table.Column<int>(type: "integer", nullable: false),
                    HintTakenAtGuess = table.Column<int>(type: "integer", nullable: true),
                    Result = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameRecords_Players_MysteryPlayerId",
                        column: x => x.MysteryPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameRecords_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameRecords_MysteryPlayerId",
                table: "GameRecords",
                column: "MysteryPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameRecords_SportId_StartedAt",
                table: "GameRecords",
                columns: new[] { "SportId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameRecords");
        }
    }
}
