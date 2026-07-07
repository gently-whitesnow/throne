using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TaskTrackerConnectionHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_checked_at",
                table: "task_tracker_connections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "task_tracker_connections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_status",
                table: "task_tracker_connections",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_checked_at",
                table: "task_tracker_connections");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "task_tracker_connections");

            migrationBuilder.DropColumn(
                name: "last_status",
                table: "task_tracker_connections");
        }
    }
}
