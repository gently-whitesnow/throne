using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CardAttachmentText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "intent_card_attachments");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "intent_card_attachments",
                newName: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "text",
                table: "intent_card_attachments",
                newName: "title");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "intent_card_attachments",
                type: "TEXT",
                nullable: true);
        }
    }
}
