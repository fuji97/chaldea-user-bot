using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class RestoreChatFullInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
