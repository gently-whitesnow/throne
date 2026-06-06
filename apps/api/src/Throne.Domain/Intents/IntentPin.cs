using Throne.Domain.Tags;

namespace Throne.Domain.Intents;

/// <summary>
/// A bookmark attaching an Intent to a tag-scoped «Pinned» bucket. Pins live
/// outside the Intent's status machine — pin/unpin/reorder never bump
/// <see cref="Intent.CurrentVersion"/> nor <see cref="Intent.UpdatedAt"/>.
/// </summary>
public sealed record IntentPin(
    IntentId IntentId,
    TagId ContextTagId,
    string PinSortKey,
    DateTimeOffset CreatedAt)
{
    public IntentPin WithSortKey(string newSortKey) => this with { PinSortKey = newSortKey };
}
