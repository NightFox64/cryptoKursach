using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatClient.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnChatIdDeliveryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId_DeliveryId",
                table: "Messages",
                columns: new[] { "ChatId", "DeliveryId" },
                unique: true,
                filter: "\"DeliveryId\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatId_DeliveryId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId",
                table: "Messages",
                column: "ChatId");
        }
    }
}
