using MongoDB.Bson;

namespace Throne.MigrateMongoSqlite;

internal static class TableSpecs
{
    public static IReadOnlyList<TableSpec> DocumentTables =>
    [
        Intents(),
        IntentEvents(),
        IntentLinks(),
        IntentPins(),
        IntentStatusChanges(),
        TextVersions(),
        Tags(),
        PromptParts(),
        PromptPartPatches(),
        DreamSessions(),
        Repositories(),
        PullRequestArtifacts(),
        IntentRepositoryBindings(),
        TerminalLaunches(),
        SkillModeDefaults(),
        GitLabHostSettings(),
        TerminalSettings(),
        Capabilities(),
    ];

    public static IReadOnlyList<string> TargetTables =>
        DocumentTables.Select(table => table.TargetTable).Append(AttachmentRows.TargetTable).ToArray();

    private static TableSpec Intents() => new("intents", "intents",
    [
        Id(),
        C("text", d => BsonFields.String(d, "text")),
        C("status", d => BsonFields.RequiredStatus(d, "status")),
        C("current_version", d => BsonFields.PositiveInt32(d, "current_version")),
        C("tag_ids", d => BsonFields.JsonOrDefault(d, "tag_ids", "[]")),
        C("sort_key", d => BsonFields.NonBlankString(d, "sort_key", "V")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
        C("cleanup_local_state_on_done", d => BsonFields.Bool(d, "cleanup_local_state_on_done", true)),
    ]);

    private static TableSpec IntentEvents() => new("intent_events", "intent_events",
    [
        Id(),
        C("intent_id", d => BsonFields.String(d, "intent_id")),
        C("peer_intent_id", d => BsonFields.NullableString(d, "peer_intent_id")),
        C("kind", d => BsonFields.String(d, "kind")),
        C("version", d => BsonFields.NullableInt32(d, "version")),
        C("text_change", d => BsonFields.JsonOrNull(d, "text_change")),
        C("link", d => BsonFields.JsonOrNull(d, "link")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("created_by", d => BsonFields.NullableString(d, "created_by")),
    ]);

    private static TableSpec IntentLinks() => new("intent_links", "intent_links",
    [
        Id(),
        C("from_id", d => BsonFields.String(d, "from_id")),
        C("to_id", d => BsonFields.String(d, "to_id")),
        C("blocking", d => BsonFields.Bool(d, "blocking")),
        C("author", d => BsonFields.String(d, "author")),
        C("rationale", d => BsonFields.NullableString(d, "rationale")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
    ]);

    private static TableSpec IntentPins() => new("intent_pins", "intent_pins",
    [
        Id(),
        C("intent_id", d => BsonFields.String(d, "intent_id")),
        C("context_tag_id", d => BsonFields.String(d, "context_tag_id")),
        C("pin_sort_key", d => BsonFields.String(d, "pin_sort_key")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
    ]);

    private static TableSpec IntentStatusChanges() => new("intent_status_changes", "intent_status_changes",
    [
        Id(),
        C("intent_id", d => BsonFields.String(d, "intent_id")),
        C("intent_version_at_write", d => BsonFields.PositiveInt32(d, "intent_version_at_write")),
        C("from_status", d => BsonFields.RequiredStatus(d, "from_status")),
        C("to_status", d => BsonFields.RequiredStatus(d, "to_status")),
        C("source", d => BsonFields.String(d, "source")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("created_by", d => BsonFields.String(d, "created_by")),
        C("reason", d => BsonFields.NullableString(d, "reason")),
    ]);

    private static TableSpec TextVersions() => new("text_versions", "text_versions",
    [
        Id(),
        C("owner_kind", d => BsonFields.String(d, "owner_kind")),
        C("owner_id", d => BsonFields.String(d, "owner_id")),
        C("version", d => BsonFields.Int32(d, "version")),
        C("kind", d => BsonFields.String(d, "kind")),
        C("snapshot", d => BsonFields.NullableString(d, "snapshot")),
        C("old_text", d => BsonFields.NullableString(d, "old_text")),
        C("new_text", d => BsonFields.NullableString(d, "new_text")),
        C("after_line", d => BsonFields.NullableInt32(d, "after_line")),
        C("insert_text", d => BsonFields.NullableString(d, "insert_text")),
        C("changed_at", d => BsonFields.DateTimeText(d, "changed_at")),
        C("changed_by", d => BsonFields.String(d, "changed_by")),
    ]);

    private static TableSpec Tags() => new("tags", "tags",
    [
        Id(),
        C("name", d => BsonFields.String(d, "name")),
        C("current_version", d => BsonFields.PositiveInt32(d, "current_version")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
        C("last_attached_at", d => BsonFields.NullableDateTimeText(d, "last_attached_at")),
        C("default_repositories", d => BsonFields.JsonOrDefault(d, "default_repositories", "[]")),
    ]);

    private static TableSpec PromptParts() => new("prompt_parts", "prompt_parts",
    [
        Id(),
        C("scope", d => BsonFields.String(d, "scope")),
        C("key", d => BsonFields.String(d, "key")),
        C("text", d => BsonFields.String(d, "text")),
        C("description", d => BsonFields.NullableString(d, "description")),
        C("current_version", d => BsonFields.PositiveInt32(d, "current_version")),
        C("mode_roles", d => BsonFields.JsonOrDefault(d, "mode_roles", "[]")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
    ]);

    private static TableSpec PromptPartPatches() => new("prompt_part_patches", "prompt_part_patches",
    [
        Id(),
        C("target_scope", d => BsonFields.String(d, "target_scope")),
        C("target_key", d => BsonFields.String(d, "target_key")),
        C("status", d => BsonFields.String(d, "status")),
        C("operation", d => BsonFields.String(d, "operation")),
        C("patch_text", d => BsonFields.String(d, "patch_text")),
        C("mode_roles", d => BsonFields.JsonOrNull(d, "mode_roles")),
        C("applied_text", d => BsonFields.NullableString(d, "applied_text")),
        C("rationale", d => BsonFields.String(d, "rationale")),
        C("reject_comment", d => BsonFields.NullableString(d, "reject_comment")),
        C("base_version", d => BsonFields.Int32(d, "base_version")),
        C("applied_version", d => BsonFields.NullableInt32(d, "applied_version")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
        C("decided_at", d => BsonFields.NullableDateTimeText(d, "decided_at")),
        C("idempotency_key", d => BsonFields.NullableString(d, "idempotency_key")),
    ]);

    private static TableSpec DreamSessions() => new("dream_sessions", "dream_sessions",
    [
        Id(),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("vendor", d => BsonFields.String(d, "vendor")),
        C("host", d => BsonFields.NullableString(d, "host")),
        C("date_from", d => BsonFields.NullableDateTimeText(d, "date_from")),
        C("date_to", d => BsonFields.NullableDateTimeText(d, "date_to")),
        C("processed_conversation_ids", d => BsonFields.JsonOrDefault(d, "processed_conversation_ids", "[]")),
        C("summary", d => BsonFields.String(d, "summary")),
        C("reflection", d => BsonFields.NullableString(d, "reflection")),
        C("proposed_patch_ids", d => BsonFields.JsonOrDefault(d, "proposed_patch_ids", "[]")),
    ]);

    private static TableSpec Repositories() => new("repositories", "repositories",
    [
        Id(),
        C("provider", d => BsonFields.String(d, "provider")),
        C("host", BsonFields.EffectiveHost),
        C("owner", d => BsonFields.String(d, "owner")),
        C("repo", d => BsonFields.String(d, "repo")),
        C("project_id", d => BsonFields.NullableInt32(d, "project_id")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
    ]);

    private static TableSpec PullRequestArtifacts() => new("pull_request_artifacts", "pull_request_artifacts",
    [
        Id(),
        C("binding_id", d => BsonFields.String(d, "binding_id")),
        C("pull_request_number", d => BsonFields.Int32(d, "pull_request_number")),
        C("type", d => BsonFields.String(d, "type")),
        C("render", d => BsonFields.String(d, "render")),
        C("content", d => BsonFields.String(d, "content")),
        C("summary", d => BsonFields.String(d, "summary")),
        C("source", d => BsonFields.String(d, "source")),
        C("source_refs", d => BsonFields.JsonOrDefault(d, "source_refs", "[]")),
        C("produced_at", d => BsonFields.DateTimeText(d, "produced_at")),
        C("head_sha", d => BsonFields.NullableString(d, "head_sha")),
        C("review_recommendation", d => BsonFields.JsonOrNull(d, "review_recommendation")),
    ]);

    private static TableSpec IntentRepositoryBindings() => new(
        "intent_repository_bindings",
        "intent_repository_bindings",
    [
        Id(),
        C("intent_id", d => BsonFields.String(d, "intent_id")),
        C("provider", d => BsonFields.String(d, "provider")),
        C("host", BsonFields.EffectiveHost),
        C("owner", d => BsonFields.String(d, "owner")),
        C("repo", d => BsonFields.String(d, "repo")),
        C("project_id", d => BsonFields.NullableInt32(d, "project_id")),
        C("default_branch", d => BsonFields.String(d, "default_branch")),
        C("workspace_path", d => BsonFields.String(d, "workspace_path")),
        C("clone_status", d => BsonFields.String(d, "clone_status")),
        C("clone_error", d => BsonFields.NullableString(d, "clone_error")),
        C("pull_request_number", d => BsonFields.NullableInt32(d, "pull_request_number")),
        C("pull_request_state", d => BsonFields.NullableString(d, "pull_request_state")),
        C("review_comments_etag", d => BsonFields.NullableString(d, "review_comments_etag")),
        C("last_seen_review_comment_at", d => BsonFields.NullableDateTimeText(d, "last_seen_review_comment_at")),
        C("last_synced_at", d => BsonFields.NullableDateTimeText(d, "last_synced_at")),
        C("created_at", d => BsonFields.DateTimeText(d, "created_at")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
        C("suppress_merge_auto_close", d => BsonFields.Bool(d, "suppress_merge_auto_close")),
    ]);

    private static TableSpec TerminalLaunches() => new("terminal_launches", "terminal_launches",
    [
        Id(),
        C("mode", d => BsonFields.String(d, "mode")),
        C("vendor", d => BsonFields.String(d, "vendor")),
        C("model", d => BsonFields.String(d, "model")),
        C("effort", d => BsonFields.NullableString(d, "effort")),
        C("attached_skill_ids", d => BsonFields.JsonOrNull(d, "attached_skill_ids")),
        C("selected_skill_ids_by_mode", d => BsonFields.JsonOrNull(d, "selected_skill_ids_by_mode")),
    ]);

    private static TableSpec SkillModeDefaults() => new("skill_mode_defaults", "skill_mode_defaults",
    [
        Id(),
        C("mode", d => BsonFields.String(d, "mode")),
        C("skill_id", d => BsonFields.String(d, "skill_id")),
        C("enabled", d => BsonFields.Bool(d, "enabled")),
    ]);

    private static TableSpec GitLabHostSettings() => new("gitlab_host_settings", "gitlab_host_settings",
    [
        Id(),
        C("host", d => BsonFields.String(d, "host")),
    ]);

    private static TableSpec TerminalSettings() => new("settings", "terminal_settings",
    [
        Id(),
        C("default_vendor", d => BsonFields.String(d, "default_vendor")),
    ], IsTerminalSettings);

    private static TableSpec Capabilities() => new("settings", "capabilities",
    [
        Id(),
        C("current_version", d => BsonFields.PositiveInt32(d, "current_version")),
        C("updated_at", d => BsonFields.DateTimeText(d, "updated_at")),
        C("selections", d => BsonFields.JsonOrDefault(d, "selections", "{}")),
    ], IsCapabilities);

    private static bool IsTerminalSettings(BsonDocument document) =>
        string.Equals(BsonFields.Id(document), "terminal", StringComparison.Ordinal);

    private static bool IsCapabilities(BsonDocument document) =>
        string.Equals(BsonFields.Id(document), "singleton", StringComparison.Ordinal);

    private static ColumnSpec Id() => C("id", BsonFields.Id);

    private static ColumnSpec C(string name, Func<BsonDocument, object?> read) => new(name, read);
}
