using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Slice2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capabilities",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    current_version = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    selections = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capabilities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dream_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    vendor = table.Column<string>(type: "TEXT", nullable: false),
                    host = table.Column<string>(type: "TEXT", nullable: true),
                    date_from = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    date_to = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    processed_conversation_ids = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    reflection = table.Column<string>(type: "TEXT", nullable: true),
                    proposed_patch_ids = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dream_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gitlab_host_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    host = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gitlab_host_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_attachments",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    file_name = table.Column<string>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    compression_state = table.Column<string>(type: "TEXT", nullable: true),
                    derived_width = table.Column<int>(type: "INTEGER", nullable: true),
                    derived_height = table.Column<int>(type: "INTEGER", nullable: true),
                    content_bytes = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    peer_intent_id = table.Column<string>(type: "TEXT", nullable: true),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: true),
                    text_change = table.Column<string>(type: "TEXT", nullable: true),
                    link = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_links",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    from_id = table.Column<string>(type: "TEXT", nullable: false),
                    to_id = table.Column<string>(type: "TEXT", nullable: false),
                    blocking = table.Column<bool>(type: "INTEGER", nullable: false),
                    author = table.Column<string>(type: "TEXT", nullable: false),
                    rationale = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_pins",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    context_tag_id = table.Column<string>(type: "TEXT", nullable: false),
                    pin_sort_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_pins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_repository_bindings",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    host = table.Column<string>(type: "TEXT", nullable: false),
                    owner = table.Column<string>(type: "TEXT", nullable: false),
                    repo = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<int>(type: "INTEGER", nullable: true),
                    default_branch = table.Column<string>(type: "TEXT", nullable: false),
                    workspace_path = table.Column<string>(type: "TEXT", nullable: false),
                    clone_status = table.Column<string>(type: "TEXT", nullable: false),
                    clone_error = table.Column<string>(type: "TEXT", nullable: true),
                    pull_request_number = table.Column<int>(type: "INTEGER", nullable: true),
                    pull_request_state = table.Column<string>(type: "TEXT", nullable: true),
                    review_comments_etag = table.Column<string>(type: "TEXT", nullable: true),
                    last_seen_review_comment_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    suppress_merge_auto_close = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_repository_bindings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_status_changes",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_id = table.Column<string>(type: "TEXT", nullable: false),
                    intent_version_at_write = table.Column<int>(type: "INTEGER", nullable: false),
                    from_status = table.Column<string>(type: "TEXT", nullable: false),
                    to_status = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_status_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intents",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    text = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    current_version = table.Column<int>(type: "INTEGER", nullable: false),
                    tag_ids = table.Column<string>(type: "TEXT", nullable: false),
                    sort_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    cleanup_local_state_on_done = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_part_patches",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    target_scope = table.Column<string>(type: "TEXT", nullable: false),
                    target_key = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    operation = table.Column<string>(type: "TEXT", nullable: false),
                    patch_text = table.Column<string>(type: "TEXT", nullable: false),
                    mode_roles = table.Column<string>(type: "TEXT", nullable: true),
                    applied_text = table.Column<string>(type: "TEXT", nullable: true),
                    rationale = table.Column<string>(type: "TEXT", nullable: false),
                    reject_comment = table.Column<string>(type: "TEXT", nullable: true),
                    base_version = table.Column<int>(type: "INTEGER", nullable: false),
                    applied_version = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    idempotency_key = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_part_patches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_parts",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    scope = table.Column<string>(type: "TEXT", nullable: false),
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    text = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    current_version = table.Column<int>(type: "INTEGER", nullable: false),
                    mode_roles = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_parts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pull_request_artifacts",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    binding_id = table.Column<string>(type: "TEXT", nullable: false),
                    pull_request_number = table.Column<int>(type: "INTEGER", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    render = table.Column<string>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    source_refs = table.Column<string>(type: "TEXT", nullable: false),
                    produced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    head_sha = table.Column<string>(type: "TEXT", nullable: true),
                    review_recommendation = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pull_request_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    host = table.Column<string>(type: "TEXT", nullable: false),
                    owner = table.Column<string>(type: "TEXT", nullable: false),
                    repo = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repositories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_mode_defaults",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    mode = table.Column<string>(type: "TEXT", nullable: false),
                    skill_id = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_mode_defaults", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    current_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_attached_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    default_repositories = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terminal_launches",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    mode = table.Column<string>(type: "TEXT", nullable: false),
                    vendor = table.Column<string>(type: "TEXT", nullable: false),
                    model = table.Column<string>(type: "TEXT", nullable: false),
                    effort = table.Column<string>(type: "TEXT", nullable: true),
                    attached_skill_ids = table.Column<string>(type: "TEXT", nullable: true),
                    selected_skill_ids_by_mode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_launches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terminal_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    default_vendor = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "text_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    owner_kind = table.Column<string>(type: "TEXT", nullable: false),
                    owner_id = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    snapshot = table.Column<string>(type: "TEXT", nullable: true),
                    old_text = table.Column<string>(type: "TEXT", nullable: true),
                    new_text = table.Column<string>(type: "TEXT", nullable: true),
                    after_line = table.Column<int>(type: "INTEGER", nullable: true),
                    insert_text = table.Column<string>(type: "TEXT", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    changed_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_versions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "dream_sessions_created_desc",
                table: "dream_sessions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "dream_sessions_host_created",
                table: "dream_sessions",
                columns: new[] { "host", "created_at" });

            migrationBuilder.CreateIndex(
                name: "dream_sessions_vendor_created",
                table: "dream_sessions",
                columns: new[] { "vendor", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_intent_attachments_intent_id",
                table: "intent_attachments",
                column: "intent_id");

            migrationBuilder.CreateIndex(
                name: "ix_intent_events_intent_created",
                table: "intent_events",
                columns: new[] { "intent_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_intent_events_peer_intent_id",
                table: "intent_events",
                column: "peer_intent_id");

            migrationBuilder.CreateIndex(
                name: "ux_intent_events_intent_text_version",
                table: "intent_events",
                columns: new[] { "intent_id", "version" },
                unique: true,
                filter: "kind = 'text_changed'");

            migrationBuilder.CreateIndex(
                name: "ix_intent_links_from_id",
                table: "intent_links",
                column: "from_id");

            migrationBuilder.CreateIndex(
                name: "ix_intent_links_to_id",
                table: "intent_links",
                column: "to_id");

            migrationBuilder.CreateIndex(
                name: "ux_intent_links_from_to",
                table: "intent_links",
                columns: new[] { "from_id", "to_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intent_pins_context_sort_key",
                table: "intent_pins",
                columns: new[] { "context_tag_id", "pin_sort_key" });

            migrationBuilder.CreateIndex(
                name: "ix_intent_pins_intent",
                table: "intent_pins",
                column: "intent_id");

            migrationBuilder.CreateIndex(
                name: "ux_intent_pins_intent_context",
                table: "intent_pins",
                columns: new[] { "intent_id", "context_tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "clone_status_pr_number",
                table: "intent_repository_bindings",
                columns: new[] { "clone_status", "pull_request_number" });

            migrationBuilder.CreateIndex(
                name: "intent_coordinate_unique",
                table: "intent_repository_bindings",
                columns: new[] { "intent_id", "provider", "host", "owner", "repo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "intent_id",
                table: "intent_repository_bindings",
                column: "intent_id");

            migrationBuilder.CreateIndex(
                name: "pr_state_last_synced_at",
                table: "intent_repository_bindings",
                columns: new[] { "pull_request_state", "last_synced_at" });

            migrationBuilder.CreateIndex(
                name: "ix_intent_status_changes_intent_created",
                table: "intent_status_changes",
                columns: new[] { "intent_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_intents_created_at_id",
                table: "intents",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_intents_sort_key",
                table: "intents",
                column: "sort_key");

            migrationBuilder.CreateIndex(
                name: "ix_intents_updated_at_id",
                table: "intents",
                columns: new[] { "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "created_desc",
                table: "prompt_part_patches",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idempotency_key_unique",
                table: "prompt_part_patches",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "status_created",
                table: "prompt_part_patches",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "target_created",
                table: "prompt_part_patches",
                columns: new[] { "target_scope", "target_key", "created_at" });

            migrationBuilder.CreateIndex(
                name: "scope_key_unique",
                table: "prompt_parts",
                columns: new[] { "scope", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "binding_id",
                table: "pull_request_artifacts",
                column: "binding_id");

            migrationBuilder.CreateIndex(
                name: "binding_type_unique",
                table: "pull_request_artifacts",
                columns: new[] { "binding_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "coordinate_unique",
                table: "repositories",
                columns: new[] { "provider", "host", "owner", "repo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "mode_skill_unique",
                table: "skill_mode_defaults",
                columns: new[] { "mode", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "created_at_id",
                table: "tags",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "last_attached_at_id",
                table: "tags",
                columns: new[] { "last_attached_at", "id" },
                filter: "last_attached_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "name_unique",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_text_versions_owner_version",
                table: "text_versions",
                columns: new[] { "owner_kind", "owner_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capabilities");

            migrationBuilder.DropTable(
                name: "dream_sessions");

            migrationBuilder.DropTable(
                name: "gitlab_host_settings");

            migrationBuilder.DropTable(
                name: "intent_attachments");

            migrationBuilder.DropTable(
                name: "intent_events");

            migrationBuilder.DropTable(
                name: "intent_links");

            migrationBuilder.DropTable(
                name: "intent_pins");

            migrationBuilder.DropTable(
                name: "intent_repository_bindings");

            migrationBuilder.DropTable(
                name: "intent_status_changes");

            migrationBuilder.DropTable(
                name: "intents");

            migrationBuilder.DropTable(
                name: "prompt_part_patches");

            migrationBuilder.DropTable(
                name: "prompt_parts");

            migrationBuilder.DropTable(
                name: "pull_request_artifacts");

            migrationBuilder.DropTable(
                name: "repositories");

            migrationBuilder.DropTable(
                name: "skill_mode_defaults");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "terminal_launches");

            migrationBuilder.DropTable(
                name: "terminal_settings");

            migrationBuilder.DropTable(
                name: "text_versions");
        }
    }
}
