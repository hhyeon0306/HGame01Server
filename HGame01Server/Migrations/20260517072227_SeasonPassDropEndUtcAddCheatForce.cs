using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HGame01Server.Migrations
{
    /// <inheritdoc />
    public partial class SeasonPassDropEndUtcAddCheatForce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seasonEndUtc",
                table: "user_season_pass");

            migrationBuilder.AddColumn<bool>(
                name: "cheatForceEnded",
                table: "user_season_pass",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cheatForceEnded",
                table: "user_season_pass");

            migrationBuilder.AddColumn<string>(
                name: "seasonEndUtc",
                table: "user_season_pass",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
