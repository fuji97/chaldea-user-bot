using Microsoft.EntityFrameworkCore.Migrations;

namespace Server.Migrations
{
    public partial class FixForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Data_Users_TelegramChatId",
                table: "Data");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Data_Key_UserId",
                table: "Data");

            migrationBuilder.DropIndex(
                name: "IX_Data_TelegramChatId",
                table: "Data");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Data");

            migrationBuilder.AddForeignKey(
                name: "FK_Data_Users_UserId",
                table: "Data",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Data_Users_UserId",
                table: "Data");

            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Data",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Data_Key_UserId",
                table: "Data",
                columns: new[] { "Key", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Data_TelegramChatId",
                table: "Data",
                column: "TelegramChatId");

            migrationBuilder.AddForeignKey(
                name: "FK_Data_Users_TelegramChatId",
                table: "Data",
                column: "TelegramChatId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
