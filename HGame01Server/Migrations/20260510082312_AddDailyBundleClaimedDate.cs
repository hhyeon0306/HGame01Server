using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HGame01Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyBundleClaimedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lastDailyBundleClaimedDateUtc",
                table: "users",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lastDailyBundleClaimedDateUtc",
                table: "users");
        }
    }
}
