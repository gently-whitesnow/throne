namespace Throne.Domain.Repositories;

/// <summary>Canonical wire-format strings for <see cref="ReviewFileRisk"/>.</summary>
public static class ReviewFileRiskNames
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";

    public static string ToWire(ReviewFileRisk value) => value switch
    {
        ReviewFileRisk.High => High,
        ReviewFileRisk.Medium => Medium,
        ReviewFileRisk.Low => Low,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown review file risk."),
    };

    public static ReviewFileRisk? TryParse(string? value) => value switch
    {
        null => null,
        High => ReviewFileRisk.High,
        Medium => ReviewFileRisk.Medium,
        Low => ReviewFileRisk.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown review file risk."),
    };
}
