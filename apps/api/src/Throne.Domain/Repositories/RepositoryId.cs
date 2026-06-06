namespace Throne.Domain.Repositories;

/// <summary>
/// Identifier for a <see cref="Repository"/> registry row. Wire-format is the raw
/// <see cref="Value"/> string (no prefix) — same convention as <c>BindingId</c>.
/// </summary>
public readonly record struct RepositoryId(string Value)
{
    public static RepositoryId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
