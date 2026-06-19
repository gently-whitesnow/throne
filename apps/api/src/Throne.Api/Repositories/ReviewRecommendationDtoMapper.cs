using Throne.Repositories.Contracts.Generated;
using DomainReviewFileOrderEntry = Throne.Domain.Repositories.ReviewFileOrderEntry;
using DomainReviewFileRisk = Throne.Domain.Repositories.ReviewFileRisk;
using DomainReviewRecommendation = Throne.Domain.Repositories.ReviewRecommendation;
using WireReviewFileRisk = Throne.Repositories.Contracts.Generated.ReviewFileRisk;

namespace Throne.Api.Repositories;

/// <summary>
/// Maps between the generated <see cref="ReviewRecommendationContent"/> DTO and the typed domain
/// <see cref="DomainReviewRecommendation"/> value object. Replaces the opaque JSON bridge from the
/// earlier draft of Slice 4C/6 — the per-type schema lives in the domain.
/// </summary>
internal static class ReviewRecommendationDtoMapper
{
    public static ReviewRecommendationContent? ToDto(DomainReviewRecommendation? recommendation)
    {
        if (recommendation is null)
        {
            return null;
        }
        return new ReviewRecommendationContent
        {
            File_order = recommendation.FileOrder
                .Select(entry => new ReviewFileOrderEntry
                {
                    Path = entry.Path,
                    Reason = entry.Reason,
                    Risk = ToWire(entry.Risk),
                })
                .ToList(),
        };
    }

    public static DomainReviewRecommendation? ToDomain(ReviewRecommendationContent? content)
    {
        if (content is null)
        {
            return null;
        }
        var fileOrder = (content.File_order ?? [])
            .Select(entry => new DomainReviewFileOrderEntry(
                entry.Path,
                entry.Reason,
                ToDomain(entry.Risk)))
            .ToArray();
        return DomainReviewRecommendation.Create(fileOrder);
    }

    private static WireReviewFileRisk? ToWire(DomainReviewFileRisk? risk) => risk switch
    {
        null => null,
        DomainReviewFileRisk.High => WireReviewFileRisk.High,
        DomainReviewFileRisk.Medium => WireReviewFileRisk.Medium,
        DomainReviewFileRisk.Low => WireReviewFileRisk.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, "Unknown review file risk."),
    };

    private static DomainReviewFileRisk? ToDomain(WireReviewFileRisk? risk) => risk switch
    {
        null => null,
        WireReviewFileRisk.High => DomainReviewFileRisk.High,
        WireReviewFileRisk.Medium => DomainReviewFileRisk.Medium,
        WireReviewFileRisk.Low => DomainReviewFileRisk.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, "Unknown review file risk."),
    };
}
