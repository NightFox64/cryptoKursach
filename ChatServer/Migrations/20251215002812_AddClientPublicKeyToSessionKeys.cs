using System.Numerics;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Globalization; // Added

#nullable disable

namespace ChatServer.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPublicKeyToSessionKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SharedSecret",
                table: "SessionKeys",
                newName: "ServerPublicKey");

            migrationBuilder.AddColumn<BigInteger>(
                name: "ClientPublicKey",
                table: "SessionKeys",
                type: "numeric",
                nullable: false,
                defaultValue: BigInteger.Parse("0", NumberFormatInfo.InvariantInfo));

            migrationBuilder.AddColumn<byte[]>(
                name: "Iv",
                table: "SessionKeys",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<BigInteger>(
                name: "ServerPrivateKey",
                table: "SessionKeys",
                type: "numeric",
                nullable: false,
                defaultValue: BigInteger.Parse("0", NumberFormatInfo.InvariantInfo));

            migrationBuilder.AddColumn<byte[]>(
                name: "SymmetricKey",
                table: "SessionKeys",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientPublicKey",
                table: "SessionKeys");

            migrationBuilder.DropColumn(
                name: "Iv",
                table: "SessionKeys");

            migrationBuilder.DropColumn(
                name: "ServerPrivateKey",
                table: "SessionKeys");

            migrationBuilder.DropColumn(
                name: "SymmetricKey",
                table: "SessionKeys");

            migrationBuilder.RenameColumn(
                name: "ServerPublicKey",
                table: "SessionKeys",
                newName: "SharedSecret");
        }
    }
}
