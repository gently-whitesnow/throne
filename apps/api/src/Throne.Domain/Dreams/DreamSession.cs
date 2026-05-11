namespace Throne.Domain.Dreams;

/// <summary>
/// Immutable record of a single /dream pass performed by a frontier agent.
/// Stores only the agent's memory of the pass: which conversations it read,
/// what summary / reflection it produced, which
/// <see cref="Instructions.InstructionPatch"/> proposals it created. The
/// dialog bytes themselves never reach the server — the agent reads them
/// locally from filesystem paths declared in the manifest
/// (<c>dream_sources</c>).
///
/// Domain invariants:
///   • <see cref="OwnerUserId"/> is required (multi-tenancy — ADR-0012);
///   • <see cref="Vendor"/> is a free-form short identifier (claude-code,
///     claude-desktop, codex-cli, …); the server does not enumerate known
///     vendors at the domain level — validation against <c>dream_sources</c>
///     happens in Application;
///   • <see cref="Summary"/> is required, ≤ <see cref="MaxSummaryLength"/> chars;
///   • <see cref="Reflection"/> is optional, ≤ <see cref="MaxReflectionLength"/> chars;
///   • <see cref="ProcessedConversationIds"/> contains at most
///     <see cref="MaxProcessedConversationIds"/> opaque strings;
///   • <see cref="ProposedPatchIds"/> contains at most
///     <see cref="MaxProposedPatchIds"/> opaque strings.
///
/// Records are append-only — there is no update path. If a user wants to amend
/// reflection, the agent records a fresh session.
///
/// Construction goes through <see cref="DreamSessionFactory"/> so the aggregate
/// itself stays inside the per-type CA1502 budget.
/// </summary>
public sealed class DreamSession
{
    public const int MaxVendorLength = 64;
    public const int MaxSummaryLength = 4000;
    public const int MaxReflectionLength = 4000;
    public const int MaxConversationIdLength = 512;
    public const int MaxProcessedConversationIds = 500;
    public const int MaxPatchIdLength = 64;
    public const int MaxProposedPatchIds = 50;

    internal DreamSession(DreamSessionIdentity identity, DreamSessionPayload payload)
    {
        Identity = identity;
        Payload = payload;
    }

    public DreamSessionIdentity Identity { get; }
    public DreamSessionPayload Payload { get; }

    public string Id => Identity.Id;
    public string OwnerUserId => Identity.OwnerUserId;

    public static DreamSession Create(
        string id,
        string ownerUserId,
        DateTimeOffset createdAt,
        string vendor,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        IReadOnlyList<string> processedConversationIds,
        string summary,
        string? reflection,
        IReadOnlyList<string> proposedPatchIds)
        => DreamSessionFactory.Create(new DreamSessionCreateInput(
            id, ownerUserId, createdAt, vendor, dateFrom, dateTo,
            processedConversationIds, summary, reflection, proposedPatchIds));

    public static DreamSession Restore(
        string id,
        string ownerUserId,
        DateTimeOffset createdAt,
        string vendor,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        IReadOnlyList<string> processedConversationIds,
        string summary,
        string? reflection,
        IReadOnlyList<string> proposedPatchIds)
        => DreamSessionFactory.Restore(new DreamSessionCreateInput(
            id, ownerUserId, createdAt, vendor, dateFrom, dateTo,
            processedConversationIds, summary, reflection, proposedPatchIds));
}

/// <summary>Identity triple stored on every <see cref="DreamSession"/>.</summary>
public sealed record DreamSessionIdentity(
    string Id,
    string OwnerUserId,
    DateTimeOffset CreatedAt);

/// <summary>Mutable-shape payload of a recorded <see cref="DreamSession"/>.</summary>
public sealed record DreamSessionPayload(
    string Vendor,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    IReadOnlyList<string> ProcessedConversationIds,
    string Summary,
    string? Reflection,
    IReadOnlyList<string> ProposedPatchIds);

/// <summary>
/// All inputs required to materialise a <see cref="DreamSession"/> via the
/// <see cref="DreamSessionFactory"/>. Folded into a record so the factory's
/// per-method cyclomatic complexity stays inside CA1502.
/// </summary>
public sealed record DreamSessionCreateInput(
    string Id,
    string OwnerUserId,
    DateTimeOffset CreatedAt,
    string Vendor,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    IReadOnlyList<string> ProcessedConversationIds,
    string Summary,
    string? Reflection,
    IReadOnlyList<string> ProposedPatchIds);
