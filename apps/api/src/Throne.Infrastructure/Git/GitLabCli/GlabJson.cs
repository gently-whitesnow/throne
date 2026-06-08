using System.Text.Json;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabJson
{
    public static string? String(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static bool Bool(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    public static int? Int(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    public static DateTimeOffset? Timestamp(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.TryGetDateTimeOffset(out var parsed) ? parsed : null;
    }

    public static string? NestedString(JsonElement parent, string objectProperty, string field) =>
        parent.TryGetProperty(objectProperty, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? String(nested, field)
            : null;
}
