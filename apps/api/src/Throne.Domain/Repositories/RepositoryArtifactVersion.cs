namespace Throne.Domain.Repositories;

/// <summary>
/// Append-only full snapshot of a <see cref="RepositoryArtifact"/> at one version
/// (ADR-0031). Knowledge pages are edited rarely, so a full snapshot per version is kept
/// instead of the delta format of <c>text_versions</c> (ADR-0002) — restore is O(1) and
/// the storage cost is negligible.
/// </summary>
public sealed record RepositoryArtifactVersion(
    string Id,
    RepositoryArtifactId ArtifactId,
    int Version,
    string Title,
    string Document,
    string RenderHint,
    DateTimeOffset CreatedAt)
{
    /// <summary>Capture the current state of <paramref name="artifact"/> as a version row.</summary>
    public static RepositoryArtifactVersion Capture(RepositoryArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new RepositoryArtifactVersion(
            Id: Guid.NewGuid().ToString("N"),
            ArtifactId: artifact.Id,
            Version: artifact.Version,
            Title: artifact.Title,
            Document: artifact.Document,
            RenderHint: artifact.RenderHint,
            CreatedAt: artifact.UpdatedAt);
    }
}
