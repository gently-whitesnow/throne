namespace Throne.Domain.Dreams;

/// <summary>
/// Top-level guard entry-point for <see cref="DreamSession.Create"/>. Delegates
/// the bulk of branching to per-aspect validators so each helper stays inside
/// the per-type CA1502 budget.
/// </summary>
internal static class DreamSessionGuards
{
    public static void EnsureCreateInputs(
        string id,
        string ownerUserId,
        string vendor,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        IReadOnlyList<string> processedConversationIds,
        string summary,
        string? reflection,
        IReadOnlyList<string> proposedPatchIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        DreamSessionVendorGuards.EnsureValid(vendor);
        DreamSessionDateGuards.EnsureRange(dateFrom, dateTo);
        DreamSessionTextGuards.EnsureSummary(summary);
        DreamSessionTextGuards.EnsureReflection(reflection);
        DreamSessionListGuards.EnsureProcessedConversationIds(processedConversationIds);
        DreamSessionListGuards.EnsureProposedPatchIds(proposedPatchIds);
    }
}

internal static class DreamSessionVendorGuards
{
    public static void EnsureValid(string vendor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);
        if (vendor.Length > DreamSession.MaxVendorLength)
        {
            throw new ArgumentException(
                $"vendor must be ≤{DreamSession.MaxVendorLength} characters.",
                nameof(vendor));
        }
    }
}

internal static class DreamSessionDateGuards
{
    public static void EnsureRange(DateTimeOffset? dateFrom, DateTimeOffset? dateTo)
    {
        if (dateFrom is { } from && dateTo is { } to && from > to)
        {
            throw new ArgumentException(
                "date_from must not be after date_to.",
                nameof(dateFrom));
        }
    }
}

internal static class DreamSessionTextGuards
{
    public static void EnsureSummary(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("summary must not be empty.", nameof(summary));
        }
        if (summary.Length > DreamSession.MaxSummaryLength)
        {
            throw new ArgumentException(
                $"summary must be ≤{DreamSession.MaxSummaryLength} characters.",
                nameof(summary));
        }
    }

    public static void EnsureReflection(string? reflection)
    {
        if (reflection is null)
        {
            return;
        }
        if (reflection.Length > DreamSession.MaxReflectionLength)
        {
            throw new ArgumentException(
                $"reflection must be ≤{DreamSession.MaxReflectionLength} characters.",
                nameof(reflection));
        }
    }
}

internal static class DreamSessionListGuards
{
    public static void EnsureProcessedConversationIds(IReadOnlyList<string> processedConversationIds)
    {
        ArgumentNullException.ThrowIfNull(processedConversationIds);
        if (processedConversationIds.Count > DreamSession.MaxProcessedConversationIds)
        {
            throw new ArgumentException(
                $"processed_conversation_ids must contain at most {DreamSession.MaxProcessedConversationIds} entries.",
                nameof(processedConversationIds));
        }
        DreamSessionListEntryGuards.EnsureEntries(
            processedConversationIds,
            DreamSession.MaxConversationIdLength,
            "processed_conversation_ids",
            nameof(processedConversationIds));
    }

    public static void EnsureProposedPatchIds(IReadOnlyList<string> proposedPatchIds)
    {
        ArgumentNullException.ThrowIfNull(proposedPatchIds);
        if (proposedPatchIds.Count > DreamSession.MaxProposedPatchIds)
        {
            throw new ArgumentException(
                $"proposed_patch_ids must contain at most {DreamSession.MaxProposedPatchIds} entries.",
                nameof(proposedPatchIds));
        }
        DreamSessionListEntryGuards.EnsureEntries(
            proposedPatchIds,
            DreamSession.MaxPatchIdLength,
            "proposed_patch_ids",
            nameof(proposedPatchIds));
    }
}

internal static class DreamSessionListEntryGuards
{
    public static void EnsureEntries(IReadOnlyList<string> entries, int maxEntryLength, string fieldName, string paramName)
    {
        foreach (var entry in entries)
        {
            EnsureSingle(entry, maxEntryLength, fieldName, paramName);
        }
    }

    private static void EnsureSingle(string entry, int maxEntryLength, string fieldName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            throw new ArgumentException($"{fieldName} entries must be non-empty.", paramName);
        }
        if (entry.Length > maxEntryLength)
        {
            throw new ArgumentException(
                $"{fieldName} entries must be ≤{maxEntryLength} characters.",
                paramName);
        }
    }
}
