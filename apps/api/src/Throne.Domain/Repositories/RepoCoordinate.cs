namespace Throne.Domain.Repositories;

/// <summary>
/// Provider-qualified <c>(provider, owner, repo)</c> coordinate. Together with
/// <c>intent_id</c> forms the unique key per ADR-0024 — same triple cannot be bound
/// twice to the same intent.
/// </summary>
public sealed record RepoCoordinate
{
    public RepoCoordinate(string Provider, string Owner, string Repo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(Repo);

        if (!GitProviderNames.IsKnown(Provider))
        {
            throw new ArgumentOutOfRangeException(nameof(Provider), $"Unknown git provider: {Provider}.");
        }

        this.Provider = Provider;
        this.Owner = Owner;
        this.Repo = Repo;
    }

    public string Provider { get; }
    public string Owner { get; }
    public string Repo { get; }

    /// <summary>Convenience <c>{owner}/{repo}</c> rendering, mirrors the contract DTO.</summary>
    public string FullName => $"{Owner}/{Repo}";
}
