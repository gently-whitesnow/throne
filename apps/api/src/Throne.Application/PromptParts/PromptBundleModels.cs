using System.Text.Json.Serialization;

namespace Throne.Application.PromptParts;

public sealed record GetPromptBundleQuery(string Mode, string? IntentId);

public sealed record PromptBundle(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("intent_id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? IntentId,
    [property: JsonPropertyName("parts")] IReadOnlyList<PromptBundlePart> Parts,
    [property: JsonPropertyName("missing_keys")] IReadOnlyList<string> MissingKeys);

public sealed record PromptBundlePart(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("prompt_part_id")] string PromptPartId,
    [property: JsonPropertyName("current_version")] int CurrentVersion,
    [property: JsonPropertyName("text")] string Text);
