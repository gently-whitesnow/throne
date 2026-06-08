namespace Throne.Domain.Repositories;

/// <summary>
/// Provider-qualified <c>(provider, host, owner, repo)</c> coordinate. Together with
/// <c>intent_id</c> forms the unique key per ADR-0024 — same triple cannot be bound
/// twice to the same intent.
///
/// Owner/repo are validated against provider-aware allowlist rules so path-traversal
/// attempts and workspace-layout collisions are rejected at the domain boundary —
/// see <see cref="RepoCoordinateGuards"/>.
/// </summary>
public sealed record RepoCoordinate
{
    public RepoCoordinate(string Provider, string Owner, string Repo, string? Host = null, int? ProjectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(Repo);

        if (!GitProviderNames.IsKnown(Provider))
        {
            throw new ArgumentOutOfRangeException(nameof(Provider), $"Unknown git provider: {Provider}.");
        }

        RepoCoordinateGuards.EnsureValidOwner(Provider, Owner);
        RepoCoordinateGuards.EnsureValidRepo(Provider, Repo);
        var host = NormalizeHost(Provider, Host);
        EnsureValidProjectId(Provider, ProjectId);

        this.Provider = Provider;
        this.Host = host;
        this.Owner = Owner;
        this.Repo = Repo;
        this.ProjectId = ProjectId;
    }

    public string Provider { get; }
    public string Host { get; }
    public string Owner { get; }
    public string Repo { get; }
    public int? ProjectId { get; }

    /// <summary>Convenience <c>{owner}/{repo}</c> rendering, mirrors the contract DTO.</summary>
    public string FullName => $"{Owner}/{Repo}";

    private static string NormalizeHost(string provider, string? host)
    {
        if (provider == GitProviderNames.GitHub)
        {
            if (!string.IsNullOrWhiteSpace(host)
                && !string.Equals(host.Trim(), GitProviderHostDefaults.GitHub, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("GitHub host is fixed to github.com.", nameof(host));
            }
            return GitProviderHostDefaults.GitHub;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var normalized = host.Trim().ToLowerInvariant();
        if (normalized.Contains("://", StringComparison.Ordinal)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("GitLab host must be a bare hostname, without scheme or path.", nameof(host));
        }
        return normalized;
    }

    private static void EnsureValidProjectId(string provider, int? projectId)
    {
        if (provider == GitProviderNames.GitHub && projectId is not null)
        {
            throw new ArgumentException("GitHub coordinates do not carry GitLab project_id.", nameof(projectId));
        }
        if (projectId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(projectId), "project_id must be positive.");
        }
    }
}
