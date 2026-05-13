using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HGame01Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonPassTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_season_pass",
                columns: table => new
                {
                    uid = table.Column<long>(type: "bigint", nullable: false),
                    seasonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currentExp = table.Column<int>(type: "int", nullable: false),
                    isPremium = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    claimedBasicJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claimedPremiumJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    seasonEndUtc = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updatedAtUtc = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_season_pass", x => new { x.uid, x.seasonId });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_season_pass_stage_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    uid = table.Column<long>(type: "bigint", nullable: false),
                    stageRunId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    seasonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    stageResult = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expGained = table.Column<int>(type: "int", nullable: false),
                    appliedAtUtc = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_season_pass_stage_runs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_season_pass_stage_runs_appliedAtUtc",
                table: "user_season_pass_stage_runs",
                column: "appliedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_user_season_pass_stage_runs_uid_stageRunId",
                table: "user_season_pass_stage_runs",
                columns: new[] { "uid", "stageRunId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_season_pass");

            migrationBuilder.DropTable(
                name: "user_season_pass_stage_runs");
        }
    }
}
