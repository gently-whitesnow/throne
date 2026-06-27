using System.Text.Json;
using System.Text.Json.Serialization;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

/// <summary>
/// Shared serializer settings for the Kaiten wire format. Kaiten speaks snake_case JSON, so the
/// adapter's PascalCase records map through <see cref="JsonNamingPolicy.SnakeCaseLower"/> in both
/// directions — no per-property attributes. <see cref="JsonIgnoreCondition.WhenWritingNull"/> makes
/// the partial-update requests (optional fields left null) omit untouched fields, so a PATCH only
/// carries what the caller set.
/// </summary>
internal static class KaitenJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
