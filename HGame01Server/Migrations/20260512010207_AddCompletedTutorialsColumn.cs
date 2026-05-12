using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HGame01Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedTutorialsColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "completedTutorialsJson",
                table: "users",
                type: "longtext",
                nullable: false,
                defaultValue: "[]")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completedTutorialsJson",
                table: "users");
        }
    }
}
