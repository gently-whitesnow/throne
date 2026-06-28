using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntentTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "intents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "title",
                table: "intents");
        }
    }
}
