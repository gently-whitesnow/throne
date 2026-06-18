using System.Text.RegularExpressions;

namespace Throne.Domain.Repositories;

/// <summary>
/// One-shot verification/review artifact anchored to a pull request binding (ADR-0031).
/// Latest write wins per <c>(binding_id, type)</c>; no version history is modelled here.
/// </summary>
public sealed partial class PullRequestArtifact
{
    public const int MaxTypeLength = 80;
    public const int MaxSummaryLength = 300;
    public const int MaxSourceRefLength = 500;
    public const int MaxSourceRefCount = 50;

    private PullRequestArtifact(
        PullRequestArtifactId id,
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt)
    {
        Id = id;
        BindingId = bindingId;
        PullRequestNumber = pullRequestNumber;
        Type = type;
        Render = render;
        Content = content;
        Summary = summary;
        Source = source;
        SourceRefs = sourceRefs;
        ProducedAt = producedAt;
    }

    public PullRequestArtifactId Id { get; }
    public BindingId BindingId { get; }
    public int PullRequestNumber { get; }
    public string Type { get; }
    public string Render { get; private set; }
    public string Content { get; private set; }
    public string Summary { get; private set; }
    public string Source { get; private set; }
    public IReadOnlyList<string> SourceRefs { get; private set; }
    public DateTimeOffset ProducedAt { get; private set; }

    public static PullRequestArtifact Create(
        PullRequestArtifactId id,
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt)
    {
        EnsureValidBindingId(bindingId);
        EnsurePositivePullRequestNumber(pullRequestNumber);
        EnsureValidType(type);
        EnsureKnownRender(render);
        ArgumentNullException.ThrowIfNull(content);
        EnsureValidSummary(summary);
        EnsureKnownSource(source);
        var refs = NormalizeSourceRefs(sourceRefs);

        return new PullRequestArtifact(
            id, bindingId, pullRequestNumber, type, render, content, summary, source, refs, producedAt);
    }

    public static PullRequestArtifact Restore(PullRequestArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Create(
            snapshot.Id,
            snapshot.BindingId,
            snapshot.PullRequestNumber,
            snapshot.Type,
            snapshot.Render,
            snapshot.Content,
            snapshot.Summary,
            snapshot.Source,
            snapshot.SourceRefs,
            snapshot.ProducedAt);
    }

    public void Replace(
        int pullRequestNumber,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt)
    {
        EnsurePositivePullRequestNumber(pullRequestNumber);
        if (pullRequestNumber != PullRequestNumber)
        {
            throw new InvalidOperationException("Cannot move a pull request artifact between pull requests.");
        }
        EnsureKnownRender(render);
        ArgumentNullException.ThrowIfNull(content);
        EnsureValidSummary(summary);
        EnsureKnownSource(source);

        Render = render;
        Content = content;
        Summary = summary;
        Source = source;
        SourceRefs = NormalizeSourceRefs(sourceRefs);
        ProducedAt = producedAt;
    }

    private static void EnsureValidBindingId(BindingId bindingId)
    {
        if (string.IsNullOrWhiteSpace(bindingId.Value))
        {
            throw new ArgumentException("binding_id is required.", nameof(bindingId));
        }
    }

    private static void EnsurePositivePullRequestNumber(int value)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "pull_request_number must be >= 1.");
        }
    }

    private static void EnsureValidType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (type.Length > MaxTypeLength || !TypePattern().IsMatch(type))
        {
            throw new ArgumentException("type must be a URL-safe label.", nameof(type));
        }
    }

    private static void EnsureKnownRender(string render)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(render);
        if (!PullRequestArtifactRenderNames.IsKnown(render))
        {
            throw new ArgumentOutOfRangeException(nameof(render), $"Unknown render: {render}.");
        }
    }

    private static void EnsureValidSummary(string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (summary.Length > MaxSummaryLength)
        {
            throw new ArgumentException($"summary exceeds {MaxSummaryLength} characters.", nameof(summary));
        }
    }

    private static void EnsureKnownSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!PullRequestArtifactSourceNames.IsKnown(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), $"Unknown source: {source}.");
        }
    }

    private static string[] NormalizeSourceRefs(IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(sourceRefs);
        if (sourceRefs.Count > MaxSourceRefCount)
        {
            throw new ArgumentException($"source_refs exceeds {MaxSourceRefCount} items.", nameof(sourceRefs));
        }
        foreach (var reference in sourceRefs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reference);
            if (reference.Length > MaxSourceRefLength)
            {
                throw new ArgumentException($"source_refs item exceeds {MaxSourceRefLength} characters.", nameof(sourceRefs));
            }
        }
        return sourceRefs.ToArray();
    }

    [GeneratedRegex("^[a-z][a-z0-9_-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex TypePattern();
}
