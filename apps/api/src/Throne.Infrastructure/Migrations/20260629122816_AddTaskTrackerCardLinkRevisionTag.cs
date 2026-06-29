using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTrackerCardLinkRevisionTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "revision_tag",
                table: "task_tracker_card_links",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "revision_tag",
                table: "task_tracker_card_links");
        }
    }
}
