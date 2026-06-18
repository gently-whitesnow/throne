using System.Text.Json;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Bridges the typed <see cref="ReviewRecommendationContent"/> contract DTO and the opaque JSON
/// string the domain stores. The per-type schema lives in the contract; the domain stays render-agnostic
/// (ADR-0031). Property names come from the generated <c>JsonPropertyName</c> attributes, so default
/// serializer options are correct.
/// </summary>
internal static class ReviewRecommendationContentJson
{
    public static string? Serialize(ReviewRecommendationContent? content) =>
        content is null ? null : JsonSerializer.Serialize(content);

    public static ReviewRecommendationContent? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<ReviewRecommendationContent>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
