namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Snake_case SQLite table names used by logs / migrations / ops tooling. The
/// constants are grouped into nested clusters (Intents/Tags/PromptParts/Repositories/
/// Terminals/Dreams) to keep the per-type public-member budget happy. The «root» table
/// inside each cluster is exposed as <c>Table</c> to avoid C# CS0542 (member-same-as-type).
/// </summary>
internal static class EfTableNames
{
    public static class Intents
    {
        public const string Table = "intents";
        public const string TextVersions = "text_versions";
        public const string IntentStatusChanges = "intent_status_changes";
        public const string IntentEvents = "intent_events";
        public const string IntentLinks = "intent_links";
        public const string IntentPins = "intent_pins";
        public const string IntentAttachments = "intent_attachments";
    }

    public static class Tags
    {
        public const string Table = "tags";
    }

    public static class PromptParts
    {
        public const string Table = "prompt_parts";
        public const string PromptPartPatches = "prompt_part_patches";
    }

    public static class Repositories
    {
        public const string Table = "repositories";
        public const string PullRequestArtifacts = "pull_request_artifacts";
        public const string IntentRepositoryBindings = "intent_repository_bindings";
    }

    public static class Terminals
    {
        public const string TerminalSettings = "terminal_settings";
        public const string TerminalLaunches = "terminal_launches";
        public const string SkillModeDefaults = "skill_mode_defaults";
        public const string Capabilities = "capabilities";
    }

    public static class Dreams
    {
        public const string DreamSessions = "dream_sessions";
    }

    public static class GitLab
    {
        public const string GitLabHostSettings = "gitlab_host_settings";
    }

    public static class TaskTrackers
    {
        public const string Connections = "task_tracker_connections";
    }
}
