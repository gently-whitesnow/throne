using System.Text.Json.Serialization;

namespace Throne.Application.Instructions;

public sealed record GetSkillsTreeQuery;

public sealed record SkillsTree(
    [property: JsonPropertyName("skills")] IReadOnlyList<SkillNode> Skills);

public sealed record SkillNode(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("launcher_body")] string LauncherBody,
    [property: JsonPropertyName("bundle")] BundleNode Bundle);

public sealed record BundleNode(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("includes")] IReadOnlyList<BundleEntryNode> Includes);

public sealed record BundleEntryNode(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("instruction_id")] string? InstructionId,
    [property: JsonPropertyName("current_version")] int CurrentVersion,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("editable")] bool Editable,
    [property: JsonPropertyName("present")] bool Present);
