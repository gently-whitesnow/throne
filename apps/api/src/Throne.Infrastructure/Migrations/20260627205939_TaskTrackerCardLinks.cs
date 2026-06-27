using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TaskTrackerCardLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_tracker_card_links",
                columns: table => new
                {
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    tracker = table.Column<string>(type: "TEXT", nullable: false),
                    board_id = table.Column<string>(type: "TEXT", nullable: false),
                    card_id = table.Column<string>(type: "TEXT", nullable: false),
                    column_id = table.Column<string>(type: "TEXT", nullable: true),
                    column_title = table.Column<string>(type: "TEXT", nullable: true),
                    snapshot_title = table.Column<string>(type: "TEXT", nullable: false),
                    snapshot_description = table.Column<string>(type: "TEXT", nullable: true),
                    card_updated_at = table.Column<string>(type: "TEXT", nullable: true),
                    column_changed_at = table.Column<string>(type: "TEXT", nullable: true),
                    last_synced_at = table.Column<string>(type: "TEXT", nullable: true),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_tracker_card_links", x => x.intent_id);
                });

            migrationBuilder.CreateIndex(
                name: "tracker_board",
                table: "task_tracker_card_links",
                columns: new[] { "tracker", "board_id" });

            migrationBuilder.CreateIndex(
                name: "tracker_board_card",
                table: "task_tracker_card_links",
                columns: new[] { "tracker", "board_id", "card_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_tracker_card_links");
        }
    }
}
