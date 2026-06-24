using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Per-intent persisted launch axis (ADR-0041) in the
/// <see cref="MongoCollectionNames.TerminalLaunches"/> collection. <c>_id</c> is the intent id —
/// one record per intent, upserted on each successful spawn. Liveness is never stored here; it
/// stays <c>tmux has-session</c>-derived. Values are wire strings (snake_case), no enum codes.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class IntentTerminalLaunchDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("effort")]
    [BsonIgnoreIfNull]
    public string? Effort { get; set; }

    /// <summary>
    /// Session skills hot-attached into the live tmux session through
    /// <c>POST /terminal/skills/attach</c>. Runtime indicator — drives the live-session
    /// badges in <c>SkillsAttachControl</c>; preflight defaults are sourced from
    /// <see cref="SelectedSkillIdsByMode"/> instead.
    /// </summary>
    [BsonElement("attached_skill_ids")]
    [BsonIgnoreIfNull]
    public List<string>? AttachedSkillIds { get; set; }

    /// <summary>
    /// Per-mode skill selection persisted on every successful spawn and merged on hot-attach.
    /// Acts as «remembered» source for the launch modal: the next preflight in this mode
    /// uses these as default-on, so hot-attached skills survive a respawn without a separate
    /// store. Keys are wire mode strings (work / interview / review / dream / free).
    /// </summary>
    [BsonElement("selected_skill_ids_by_mode")]
    [BsonIgnoreIfNull]
    public Dictionary<string, List<string>>? SelectedSkillIdsByMode { get; set; }
}
