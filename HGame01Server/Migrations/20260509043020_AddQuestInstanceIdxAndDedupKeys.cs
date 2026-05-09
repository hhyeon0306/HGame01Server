using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HGame01Server.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestInstanceIdxAndDedupKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_quest_instances_uid",
                table: "user_quest_instances");

            migrationBuilder.DropIndex(
                name: "IX_user_quest_events_applied_uid_eventClientId",
                table: "user_quest_events_applied");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "user_quest_instances",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "eventTypeName",
                table: "user_quest_events_applied",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "questInstanceId",
                table: "user_quest_events_applied",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_quest_instances_uid_container_status",
                table: "user_quest_instances",
                columns: new[] { "uid", "containerStableId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_quest_instances_uid_questData_status",
                table: "user_quest_instances",
                columns: new[] { "uid", "questDataId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_quest_events_applied_uid_instance_clientId",
                table: "user_quest_events_applied",
                columns: new[] { "uid", "questInstanceId", "eventClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_quest_instances_uid_container_status",
                table: "user_quest_instances");

            migrationBuilder.DropIndex(
                name: "IX_user_quest_instances_uid_questData_status",
                table: "user_quest_instances");

            migrationBuilder.DropIndex(
                name: "IX_user_quest_events_applied_uid_instance_clientId",
                table: "user_quest_events_applied");

            migrationBuilder.DropColumn(
                name: "eventTypeName",
                table: "user_quest_events_applied");

            migrationBuilder.DropColumn(
                name: "questInstanceId",
                table: "user_quest_events_applied");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "user_quest_instances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_quest_instances_uid",
                table: "user_quest_instances",
                column: "uid");

            migrationBuilder.CreateIndex(
                name: "IX_user_quest_events_applied_uid_eventClientId",
                table: "user_quest_events_applied",
                columns: new[] { "uid", "eventClientId" },
                unique: true);
        }
    }
}
