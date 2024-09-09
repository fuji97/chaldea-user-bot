using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChatModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "TelegramChats");

            migrationBuilder.DropColumn(
                name: "InviteLink",
                table: "TelegramChats");

            migrationBuilder.DropColumn(
                name: "StickerSetName",
                table: "TelegramChats");

            migrationBuilder.RenameColumn(
                name: "CanSetStickerSet",
                table: "TelegramChats",
                newName: "IsForum");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Newsletters",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsForum",
                table: "TelegramChats",
                newName: "CanSetStickerSet");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TelegramChats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteLink",
                table: "TelegramChats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StickerSetName",
                table: "TelegramChats",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Newsletters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
