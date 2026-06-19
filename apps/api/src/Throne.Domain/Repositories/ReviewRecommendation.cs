namespace Throne.Domain.Repositories;

/// <summary>
/// Typed payload for artifacts of <c>type=review_recommendation</c> (ADR-0031). Lives in the
/// domain so the per-type schema is validated where the data is owned, not at the wire boundary.
/// Optional alongside the artifact: only the AI <see cref="FileOrder"/> is modelled today; impact
/// and provenance fields will be added when a UI consumer materialises.
/// </summary>
public sealed record ReviewRecommendation(IReadOnlyList<ReviewFileOrderEntry> FileOrder)
{
    public const int MaxFileOrderEntries = 2000;

    public static ReviewRecommendation Create(IReadOnlyList<ReviewFileOrderEntry>? fileOrder)
    {
        var entries = fileOrder ?? [];
        if (entries.Count > MaxFileOrderEntries)
        {
            throw new ArgumentException(
                $"file_order exceeds {MaxFileOrderEntries} items.",
                nameof(fileOrder));
        }
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
        }
        return new ReviewRecommendation(entries.ToArray());
    }
}

/// <summary>One file in the AI reading order (most risky/root → leaves).</summary>
public sealed record ReviewFileOrderEntry
{
    public const int MaxPathLength = 500;
    public const int MaxReasonLength = 500;

    public ReviewFileOrderEntry(string path, string? reason, ReviewFileRisk? risk)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length > MaxPathLength)
        {
            throw new ArgumentException($"path exceeds {MaxPathLength} characters.", nameof(path));
        }
        if (reason is not null && reason.Length > MaxReasonLength)
        {
            throw new ArgumentException($"reason exceeds {MaxReasonLength} characters.", nameof(reason));
        }
        Path = path;
        Reason = reason;
        Risk = risk;
    }

    public string Path { get; }
    public string? Reason { get; }
    public ReviewFileRisk? Risk { get; }
}

public enum ReviewFileRisk
{
    High,
    Medium,
    Low,
}
