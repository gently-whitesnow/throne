using System.Text.Json.Serialization;

namespace Throne.Application.PromptParts;

public sealed record GetBundlesTreeQuery;

public sealed record BundlesTree(
    [property: JsonPropertyName("bundles")] IReadOnlyList<BundleNode> Bundles);

public sealed record BundleNode(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("includes")] IReadOnlyList<BundleEntryNode> Includes);

public sealed record BundleEntryNode(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("prompt_part_id")] string? PromptPartId,
    [property: JsonPropertyName("current_version")] int CurrentVersion,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("editable")] bool Editable,
    [property: JsonPropertyName("present")] bool Present);
