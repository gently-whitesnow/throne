using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CardMirrorToAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_tracker_card_links");

            migrationBuilder.CreateTable(
                name: "intent_card_attachments",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    tracker = table.Column<string>(type: "TEXT", nullable: false),
                    board_id = table.Column<string>(type: "TEXT", nullable: false),
                    card_id = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    column_title = table.Column<string>(type: "TEXT", nullable: true),
                    archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    card_version = table.Column<string>(type: "TEXT", nullable: true),
                    availability = table.Column<string>(type: "TEXT", nullable: false),
                    fetched_at = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_card_attachments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "intent_card_unique",
                table: "intent_card_attachments",
                columns: new[] { "intent_id", "tracker", "board_id", "card_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intent_card_attachments_board",
                table: "intent_card_attachments",
                columns: new[] { "tracker", "board_id" });

            migrationBuilder.CreateIndex(
                name: "ix_intent_card_attachments_intent_id",
                table: "intent_card_attachments",
                column: "intent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intent_card_attachments");

            migrationBuilder.CreateTable(
                name: "task_tracker_card_links",
                columns: table => new
                {
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    board_id = table.Column<string>(type: "TEXT", nullable: false),
                    card_id = table.Column<string>(type: "TEXT", nullable: false),
                    card_updated_at = table.Column<string>(type: "TEXT", nullable: true),
                    column_changed_at = table.Column<string>(type: "TEXT", nullable: true),
                    column_id = table.Column<string>(type: "TEXT", nullable: true),
                    column_title = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    last_synced_at = table.Column<string>(type: "TEXT", nullable: true),
                    revision_tag = table.Column<string>(type: "TEXT", nullable: true),
                    snapshot_description = table.Column<string>(type: "TEXT", nullable: true),
                    snapshot_title = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    tracker = table.Column<string>(type: "TEXT", nullable: false),
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
    }
}
