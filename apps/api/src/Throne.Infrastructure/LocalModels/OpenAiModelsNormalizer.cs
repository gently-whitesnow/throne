using System.Text.Json;
using System.Text.Json.Serialization;

namespace Throne.Infrastructure.LocalModels;

/// <summary>
/// Pure normalization of an OpenAI-compatible <c>GET /v1/models</c> payload into deduplicated,
/// non-empty model ids in advertised order. Kept separate from the HTTP adapter so the parsing
/// rules are unit-testable without I/O.
/// </summary>
internal static class OpenAiModelsNormalizer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> Normalize(string payload)
    {
        var parsed = JsonSerializer.Deserialize<ModelsResponse>(payload, Options);
        var data = parsed?.Data;
        if (data is null || data.Count == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ids = new List<string>(data.Count);
        foreach (var entry in data)
        {
            var id = entry.Id?.Trim();
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private sealed record ModelsResponse([property: JsonPropertyName("data")] IReadOnlyList<ModelEntry>? Data);

    private sealed record ModelEntry([property: JsonPropertyName("id")] string? Id);
}
