using Throne.Domain.Tags;

namespace Throne.Domain.Intents;

public static class IntentFactory
{
    public static Intent Create(
        IntentId id,
        string ownerUserId,
        string text,
        IReadOnlyList<TagId>? tagIds,
        DateTimeOffset now,
        string? sortKey = null)
    {
        IntentGuards.EnsureCreateInputs(ownerUserId, text);
        var resolvedSortKey = sortKey ?? FractionalIndex.Initial();
        FractionalIndex.ValidateKey(resolvedSortKey, nameof(sortKey));
        var normalized = TagIdSet.Normalize(tagIds);
        var state = new IntentState(text, IntentStatusNames.Draft, CurrentVersion: 1, resolvedSortKey, now);
        return new Intent(id, ownerUserId, state, normalized, now);
    }

    public static Intent Restore(
        IntentId id,
        string ownerUserId,
        string text,
        string status,
        int currentVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? sortKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        IntentGuards.EnsureValidStatus(status, nameof(status));
        IntentGuards.EnsureValidCurrentVersion(currentVersion);
        var resolvedSortKey = sortKey ?? FractionalIndex.Initial();
        FractionalIndex.ValidateKey(resolvedSortKey, nameof(sortKey));
        var state = new IntentState(text, status, currentVersion, resolvedSortKey, updatedAt);
        return new Intent(id, ownerUserId, state, tagIds, createdAt);
    }
}
