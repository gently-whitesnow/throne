namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>tags</c> table. Mirrors <c>TagDocument</c> field-for-field;
/// <c>default_repositories</c> is materialized as a JSON array column (no join table) since
/// the list is short, replace-only, and never queried by individual entry.
/// </summary>
internal sealed class TagRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastAttachedAt { get; set; }
    public List<TagDefaultRepositoryPayload> DefaultRepositories { get; set; } = [];
}

/// <summary>
/// JSON shape for one entry inside <c>tags.default_repositories</c>. Field names match the
/// JSON subdocument 1:1 so a SQLite dump reads identically against the OpenAPI contract.
/// </summary>
internal sealed class TagDefaultRepositoryPayload
{
    public string Provider { get; set; } = string.Empty;
    public string? Host { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string? DefaultBranch { get; set; }
}
