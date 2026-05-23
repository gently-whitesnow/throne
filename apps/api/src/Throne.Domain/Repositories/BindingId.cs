namespace Throne.Domain.Repositories;

/// <summary>
/// Identifier for an <see cref="IntentRepositoryBinding"/>. Wire-format is the raw
/// <see cref="Value"/> string (no prefix) — same convention as <c>IntentId</c>.
/// </summary>
public readonly record struct BindingId(string Value)
{
    public static BindingId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
