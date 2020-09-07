using Microsoft.EntityFrameworkCore.Migrations;

namespace Server.Migrations
{
    public partial class AddChatSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Data_Users_UserId",
                table: "Data");

            migrationBuilder.DropForeignKey(
                name: "FK_Masters_Users_UserId",
                table: "Masters");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsletterChats_Users_ChatId",
                table: "NewsletterChats");

            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredChats_Users_ChatId",
                table: "RegisteredChats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "TelegramChats");

            migrationBuilder.AddColumn<bool>(
                name: "ServantListNotifications",
                table: "TelegramChats",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportListNotifications",
                table: "TelegramChats",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TelegramChats",
                table: "TelegramChats",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Data_TelegramChats_UserId",
                table: "Data",
                column: "UserId",
                principalTable: "TelegramChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Masters_TelegramChats_UserId",
                table: "Masters",
                column: "UserId",
                principalTable: "TelegramChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsletterChats_TelegramChats_ChatId",
                table: "NewsletterChats",
                column: "ChatId",
                principalTable: "TelegramChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredChats_TelegramChats_ChatId",
                table: "RegisteredChats",
                column: "ChatId",
                principalTable: "TelegramChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Data_TelegramChats_UserId",
                table: "Data");

            migrationBuilder.DropForeignKey(
                name: "FK_Masters_TelegramChats_UserId",
                table: "Masters");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsletterChats_TelegramChats_ChatId",
                table: "NewsletterChats");

            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredChats_TelegramChats_ChatId",
                table: "RegisteredChats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TelegramChats",
                table: "TelegramChats");

            migrationBuilder.DropColumn(
                name: "ServantListNotifications",
                table: "TelegramChats");

            migrationBuilder.DropColumn(
                name: "SupportListNotifications",
                table: "TelegramChats");

            migrationBuilder.RenameTable(
                name: "TelegramChats",
                newName: "Users");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Data_Users_UserId",
                table: "Data",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Masters_Users_UserId",
                table: "Masters",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsletterChats_Users_ChatId",
                table: "NewsletterChats",
                column: "ChatId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredChats_Users_ChatId",
                table: "RegisteredChats",
                column: "ChatId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
