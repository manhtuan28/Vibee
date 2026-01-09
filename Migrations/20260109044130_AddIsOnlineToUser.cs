using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibee.Migrations
{
    public partial class AddIsOnlineToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Message_AspNetUsers_ReceiverId", table: "Message");
            migrationBuilder.DropForeignKey(name: "FK_Message_AspNetUsers_SenderId", table: "Message");
            migrationBuilder.DropPrimaryKey(name: "PK_Message", table: "Message");

            migrationBuilder.RenameTable(name: "Message", newName: "Messages");

            migrationBuilder.AddColumn<bool>(name: "IsOnline", table: "AspNetUsers", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "LastActive", table: "AspNetUsers", type: "datetime2", nullable: false, defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(name: "PK_Messages", table: "Messages", column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId",
                table: "Messages",
                column: "ReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}