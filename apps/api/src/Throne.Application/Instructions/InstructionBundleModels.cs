using System.Text.Json.Serialization;

namespace Throne.Application.Instructions;

public sealed record GetInstructionBundleQuery(string Mode, string? IntentId);

public sealed record InstructionBundle(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("intent_id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? IntentId,
    [property: JsonPropertyName("instructions")] IReadOnlyList<InstructionWithText> Instructions,
    [property: JsonPropertyName("missing_kinds")] IReadOnlyList<string> MissingKinds);

public sealed record InstructionWithText(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("instruction_id")] string InstructionId,
    [property: JsonPropertyName("current_version")] int CurrentVersion,
    [property: JsonPropertyName("text")] string Text);
