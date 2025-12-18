using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedChatKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SharedG",
                table: "Chats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SharedIv",
                table: "Chats",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedP",
                table: "Chats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SharedSymmetricKey",
                table: "Chats",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharedG",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "SharedIv",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "SharedP",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "SharedSymmetricKey",
                table: "Chats");
        }
    }
}
