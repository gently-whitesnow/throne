namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>repositories</c> registry table (ADR-0031). Mirrors
/// <c>RepositoryDocument</c> with one twist: <c>host</c> is always the EFFECTIVE host
/// (GitHub default folded to <c>github.com</c>) so the unique
/// <c>(provider, host, owner, repo)</c> index never sees a NULL on the GitHub side and
/// the «default-host vs explicit-host» race cannot insert a duplicate row.
/// </summary>
internal sealed class RepositoryRow
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
