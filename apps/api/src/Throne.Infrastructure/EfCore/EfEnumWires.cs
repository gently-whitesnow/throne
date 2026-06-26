using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Wire strings for persistence-agnostic enums. The string values are the on-disk and
/// on-the-wire contract.
/// </summary>
internal static class EfEnumWires
{
    public static string ToWire(this TextVersionOwnerKind value) => value switch
    {
        TextVersionOwnerKind.Intent => "intent",
        TextVersionOwnerKind.Instruction => "instruction",
        TextVersionOwnerKind.PromptPart => "prompt_part",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static TextVersionOwnerKind ParseTextVersionOwnerKind(string wire) => wire switch
    {
        "intent" => TextVersionOwnerKind.Intent,
        "instruction" => TextVersionOwnerKind.Instruction,
        "prompt_part" => TextVersionOwnerKind.PromptPart,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown owner_kind."),
    };

    public static string ToWire(this TextVersionKind value) => value switch
    {
        TextVersionKind.Create => "create",
        TextVersionKind.Replace => "replace",
        TextVersionKind.Insert => "insert",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static TextVersionKind ParseTextVersionKind(string wire) => wire switch
    {
        "create" => TextVersionKind.Create,
        "replace" => TextVersionKind.Replace,
        "insert" => TextVersionKind.Insert,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown kind."),
    };

    public static string ToWire(this TextVersionAuthor value) => value switch
    {
        TextVersionAuthor.User => "user",
        TextVersionAuthor.Agent => "agent",
        TextVersionAuthor.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static TextVersionAuthor ParseTextVersionAuthor(string wire) => wire switch
    {
        "user" => TextVersionAuthor.User,
        "agent" => TextVersionAuthor.Agent,
        "system" => TextVersionAuthor.System,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown changed_by."),
    };

    public static string ToWire(this IntentTrainingAuthor value) => value switch
    {
        IntentTrainingAuthor.Agent => "agent",
        IntentTrainingAuthor.User => "user",
        IntentTrainingAuthor.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static IntentTrainingAuthor ParseIntentTrainingAuthor(string wire) => wire switch
    {
        "agent" => IntentTrainingAuthor.Agent,
        "user" => IntentTrainingAuthor.User,
        "system" => IntentTrainingAuthor.System,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown training author."),
    };
}
