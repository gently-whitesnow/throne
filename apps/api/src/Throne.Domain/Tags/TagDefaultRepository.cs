using Throne.Domain.Repositories;

namespace Throne.Domain.Tags;

/// <summary>
/// Repository preset attached to a <see cref="Tag"/>. Slice 2 Run pre-flight unions
/// these across all tags of an intent (deduped on <see cref="RepoCoordinate"/>) and
/// auto-binds the missing ones before spawning the agent — see the Slice 2 ADR.
///
/// Uniqueness inside one tag is enforced on the coordinate triple
/// <c>(provider, owner, repo)</c>. <see cref="DefaultBranch"/> is an optional hint
/// for the clone step; <c>null</c> means «fall back to upstream's default branch».
/// </summary>
public sealed record TagDefaultRepository
{
    public TagDefaultRepository(RepoCoordinate Coordinate, string? DefaultBranch)
    {
        ArgumentNullException.ThrowIfNull(Coordinate);
        if (DefaultBranch is not null && string.IsNullOrWhiteSpace(DefaultBranch))
        {
            throw new ArgumentException(
                "default_branch, when supplied, must not be whitespace.",
                nameof(DefaultBranch));
        }

        this.Coordinate = Coordinate;
        this.DefaultBranch = DefaultBranch;
    }

    public RepoCoordinate Coordinate { get; }
    public string? DefaultBranch { get; }
}
