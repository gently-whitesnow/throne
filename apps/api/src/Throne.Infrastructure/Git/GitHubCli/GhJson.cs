using System.Text.Json;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Tiny helpers over <see cref="JsonElement"/> used by the <c>gh</c> repo parsers.
/// Extracted so each parser stays inside the CA1502 cyclomatic budget.
/// </summary>
internal static class GhJson
{
    public static string? String(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static bool Bool(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    public static string? NestedString(JsonElement parent, string objectProperty, string field) =>
        parent.TryGetProperty(objectProperty, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? String(nested, field)
            : null;
}
