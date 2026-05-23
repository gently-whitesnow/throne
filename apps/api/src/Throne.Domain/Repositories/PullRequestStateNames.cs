namespace Throne.Domain.Repositories;

/// <summary>
/// Wire-format constants for the upstream lifecycle state of the linked pull request.
/// Mirrors <c>PullRequestState</c> in <c>specs/contracts/repositories/openapi.yaml</c>.
/// </summary>
public static class PullRequestStateNames
{
    public const string Open = "open";
    public const string Closed = "closed";
    public const string Merged = "merged";

    public static bool IsKnown(string value) =>
        value is Open or Closed or Merged;
}
