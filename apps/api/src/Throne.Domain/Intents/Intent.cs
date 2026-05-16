using Throne.Domain.Tags;

namespace Throne.Domain.Intents;

public sealed class Intent
{
    private readonly List<TagId> _tagIds;

    internal Intent(
        IntentId id,
        string ownerUserId,
        IntentState state,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        State = state;
        _tagIds = [.. tagIds];
        CreatedAt = createdAt;
    }

    public IntentId Id { get; }
    public string OwnerUserId { get; }
    public DateTimeOffset CreatedAt { get; }
    public IntentState State { get; internal set; }
    public IReadOnlyList<TagId> TagIds => _tagIds;

    internal void ReplaceTagIds(IReadOnlyList<TagId> tagIds)
    {
        _tagIds.Clear();
        _tagIds.AddRange(tagIds);
    }
}
