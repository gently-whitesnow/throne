namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Snake_case SQLite table names; mirrors <c>MongoCollectionNames</c> entry for entry so
/// the two backends speak the same vocabulary in logs / migrations / ops tooling.
/// </summary>
internal static class EfTableNames
{
    public const string Intents = "intents";
    public const string TextVersions = "text_versions";
    public const string IntentStatusChanges = "intent_status_changes";
    public const string IntentEvents = "intent_events";
    public const string IntentLinks = "intent_links";
    public const string IntentPins = "intent_pins";
    public const string IntentAttachments = "intent_attachments";
    public const string Tags = "tags";
    public const string PromptParts = "prompt_parts";
    public const string PromptPartPatches = "prompt_part_patches";
    public const string DreamSessions = "dream_sessions";
    public const string Repositories = "repositories";
    public const string PullRequestArtifacts = "pull_request_artifacts";
    public const string IntentRepositoryBindings = "intent_repository_bindings";
    public const string Capabilities = "capabilities";
    public const string TerminalSettings = "terminal_settings";
    public const string SkillModeDefaults = "skill_mode_defaults";
    public const string TerminalLaunches = "terminal_launches";
    public const string GitLabHostSettings = "gitlab_host_settings";
}
