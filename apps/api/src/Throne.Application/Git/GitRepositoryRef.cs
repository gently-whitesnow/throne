namespace Throne.Application.Git;

/// <summary>
/// Typed projection of a single repository returned by an <see cref="IGitProvider"/>.
/// Mirrors <c>GitRepositoryRefDto</c> in <c>specs/contracts/repositories/openapi.yaml</c>
/// but lives in Application to keep the port independent of generated DTOs.
/// </summary>
/// <param name="Provider">Wire-format provider name (see <c>GitProviderNames</c>).</param>
/// <param name="Owner">Repository owner (user or organisation login).</param>
/// <param name="Repo">Repository slug, without owner prefix.</param>
/// <param name="DefaultBranch">Upstream default branch name (e.g. <c>main</c>).</param>
public sealed record GitRepositoryRef(
    string Provider,
    string Owner,
    string Repo,
    string DefaultBranch)
{
    /// <summary>Short repository description, when upstream supplies one.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the upstream repository is private.</summary>
    public bool Private { get; init; }

    /// <summary>Browser-facing URL of the repository.</summary>
    public string? HtmlUrl { get; init; }

    /// <summary>Convenience <c>{owner}/{repo}</c> rendering.</summary>
    public string FullName => $"{Owner}/{Repo}";
}
