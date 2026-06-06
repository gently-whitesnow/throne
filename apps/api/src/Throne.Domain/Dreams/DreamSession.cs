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
///   • <see cref="Vendor"/> is a free-form short identifier (claude-code,
///     claude-desktop, codex-cli, …); the server does not enumerate known
///     vendors at the domain level — validation against <c>dream_sources</c>
///     happens in Application;
///   • <see cref="Payload"/>.<see cref="DreamSessionPayload.Summary"/> is
///     required, ≤ <see cref="MaxSummaryLength"/> chars;
///   • <see cref="Payload"/>.<see cref="DreamSessionPayload.Reflection"/> is
///     optional, ≤ <see cref="MaxReflectionLength"/> chars;
///   • <see cref="Payload"/>.<see cref="DreamSessionPayload.ProcessedConversationIds"/>
///     contains at most <see cref="MaxProcessedConversationIds"/> opaque strings;
///   • <see cref="Payload"/>.<see cref="DreamSessionPayload.ProposedPatchIds"/>
///     contains at most <see cref="MaxProposedPatchIds"/> opaque strings.
///
/// Records are append-only — there is no update path. If a user wants to amend
/// reflection, the agent records a fresh session.
/// </summary>
public sealed class DreamSession
{
    public const int MaxVendorLength = 64;
    public const int MaxHostLength = 255;
    public const int MaxSummaryLength = 4000;
    public const int MaxReflectionLength = 4000;
    public const int MaxConversationIdLength = 512;
    public const int MaxProcessedConversationIds = 500;
    public const int MaxPatchIdLength = 64;
    public const int MaxProposedPatchIds = 50;

    private DreamSession(DreamSessionIdentity identity, DreamSessionPayload payload)
    {
        Identity = identity;
        Payload = payload;
    }

    public DreamSessionIdentity Identity { get; }
    public DreamSessionPayload Payload { get; }

    public string Id => Identity.Id;

    public static DreamSession Create(
        string id,
        DateTimeOffset createdAt,
        string vendor,
        string host,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        IReadOnlyList<string> processedConversationIds,
        string summary,
        string? reflection,
        IReadOnlyList<string> proposedPatchIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);
        if (vendor.Length > MaxVendorLength)
        {
            throw new ArgumentException(
                $"vendor must be ≤{MaxVendorLength} characters.",
                nameof(vendor));
        }

        if (host is null || string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("host must not be empty.", nameof(host));
        }
        if (host.Length > MaxHostLength)
        {
            throw new ArgumentException(
                $"host must be ≤{MaxHostLength} characters.",
                nameof(host));
        }

        if (dateFrom is { } from && dateTo is { } to && from > to)
        {
            throw new ArgumentException(
                "date_from must not be after date_to.",
                nameof(dateFrom));
        }

        ArgumentNullException.ThrowIfNull(summary);
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("summary must not be empty.", nameof(summary));
        }
        if (summary.Length > MaxSummaryLength)
        {
            throw new ArgumentException(
                $"summary must be ≤{MaxSummaryLength} characters.",
                nameof(summary));
        }

        if (reflection is not null && reflection.Length > MaxReflectionLength)
        {
            throw new ArgumentException(
                $"reflection must be ≤{MaxReflectionLength} characters.",
                nameof(reflection));
        }

        ArgumentNullException.ThrowIfNull(processedConversationIds);
        if (processedConversationIds.Count > MaxProcessedConversationIds)
        {
            throw new ArgumentException(
                $"processed_conversation_ids must contain at most {MaxProcessedConversationIds} entries.",
                nameof(processedConversationIds));
        }
        foreach (var entry in processedConversationIds)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException(
                    "processed_conversation_ids entries must be non-empty.",
                    nameof(processedConversationIds));
            }
            if (entry.Length > MaxConversationIdLength)
            {
                throw new ArgumentException(
                    $"processed_conversation_ids entries must be ≤{MaxConversationIdLength} characters.",
                    nameof(processedConversationIds));
            }
        }

        ArgumentNullException.ThrowIfNull(proposedPatchIds);
        if (proposedPatchIds.Count > MaxProposedPatchIds)
        {
            throw new ArgumentException(
                $"proposed_patch_ids must contain at most {MaxProposedPatchIds} entries.",
                nameof(proposedPatchIds));
        }
        foreach (var entry in proposedPatchIds)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException(
                    "proposed_patch_ids entries must be non-empty.",
                    nameof(proposedPatchIds));
            }
            if (entry.Length > MaxPatchIdLength)
            {
                throw new ArgumentException(
                    $"proposed_patch_ids entries must be ≤{MaxPatchIdLength} characters.",
                    nameof(proposedPatchIds));
            }
        }

        return new DreamSession(
            new DreamSessionIdentity(id, createdAt),
            new DreamSessionPayload(
                vendor.Trim(),
                host.Trim(),
                dateFrom,
                dateTo,
                [.. processedConversationIds],
                summary,
                reflection,
                [.. proposedPatchIds]));
    }

    /// <summary>
    /// Materialise an existing record from persistence. <paramref name="host"/>
    /// may be <c>null</c> for legacy sessions written before the host field
    /// existed; new records must always carry a non-empty host.
    /// </summary>
    public static DreamSession Restore(
        string id,
        DateTimeOffset createdAt,
        string vendor,
        string? host,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        IReadOnlyList<string> processedConversationIds,
        string summary,
        string? reflection,
        IReadOnlyList<string> proposedPatchIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);
        ArgumentNullException.ThrowIfNull(processedConversationIds);
        ArgumentNullException.ThrowIfNull(proposedPatchIds);
        ArgumentNullException.ThrowIfNull(summary);

        var normalisedHost = string.IsNullOrWhiteSpace(host) ? null : host;

        return new DreamSession(
            new DreamSessionIdentity(id, createdAt),
            new DreamSessionPayload(
                vendor,
                normalisedHost,
                dateFrom,
                dateTo,
                [.. processedConversationIds],
                summary,
                reflection,
                [.. proposedPatchIds]));
    }
}

/// <summary>Identity pair stored on every <see cref="DreamSession"/>.</summary>
public sealed record DreamSessionIdentity(
    string Id,
    DateTimeOffset CreatedAt);

/// <summary>
/// Mutable-shape payload of a recorded <see cref="DreamSession"/>. <see cref="Host"/>
/// is the agent-reported machine identifier (typically the OS hostname) the
/// /dream pass ran on; it scopes the «what's already processed» frontier per
/// machine so a user running dream from home and work doesn't merge state.
/// May be <c>null</c> only for legacy records written before the field existed.
/// </summary>
public sealed record DreamSessionPayload(
    string Vendor,
    string? Host,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    IReadOnlyList<string> ProcessedConversationIds,
    string Summary,
    string? Reflection,
    IReadOnlyList<string> ProposedPatchIds);
