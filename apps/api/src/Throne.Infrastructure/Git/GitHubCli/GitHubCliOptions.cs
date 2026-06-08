namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Bind target for <c>Throne:GitHubCli</c>. Defaults pick the system-wide
/// <c>gh</c> on PATH and a 30-second timeout for API/list calls; clone gets a
/// longer budget because it may transfer many megabytes (ADR-0024 § 3 — no
/// custom OAuth, all auth flows through the user's <c>gh auth login</c>).
/// </summary>
public sealed class GitHubCliOptions
{
    public const string SectionName = "Throne:GitHubCli";

    /// <summary>Executable name or absolute path. Set to a stub in tests.</summary>
    public string ExecutablePath { get; set; } = "gh";

    /// <summary>Default timeout for short-lived API / list calls (search, auth, fetch).</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for <c>gh repo clone</c> — large monorepos take noticeably longer.</summary>
    public TimeSpan CloneTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Page size for <c>gh repo list</c> / <c>gh api /user/repos</c>.</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Upper bound on how many of the account's repos are pulled from
    /// <c>gh repo list</c> before the client-side query filter runs. Deliberately
    /// larger than the picker's display limit: ADR-0024 § 3 forbids the global
    /// <c>gh search repos</c> path, so substring matching happens in-process and
    /// must see the whole account — otherwise repos pushed long ago fall outside
    /// the recency-sorted window and become unfindable by name.
    /// </summary>
    public int SearchFetchLimit { get; set; } = 1000;
}
