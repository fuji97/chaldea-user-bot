using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class HardenChatSettingsAndMasterName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"TelegramChats\" SET \"ServantListNotifications\" = FALSE WHERE \"ServantListNotifications\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"TelegramChats\" SET \"SupportListNotifications\" = FALSE WHERE \"SupportListNotifications\" IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_Masters_UserId",
                table: "Masters");

            migrationBuilder.AlterColumn<bool>(
                name: "SupportListNotifications",
                table: "TelegramChats",
                type: "boolean",
                nullable: true,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "ServantListNotifications",
                table: "TelegramChats",
                type: "boolean",
                nullable: true,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Masters_UserId_Name",
                table: "Masters",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Masters_UserId_Name",
                table: "Masters");

            migrationBuilder.AlterColumn<bool>(
                name: "SupportListNotifications",
                table: "TelegramChats",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "ServantListNotifications",
                table: "TelegramChats",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Masters_UserId",
                table: "Masters",
                column: "UserId");
        }
    }
}
