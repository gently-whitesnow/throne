using Throne.Application.Ports;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Infrastructure.Mongo;

internal static class MongoEnumNames
{
    public static string ToWire(this TextVersionOwnerKind value) => value switch
    {
        TextVersionOwnerKind.Intent => "intent",
        TextVersionOwnerKind.Instruction => "instruction",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToWire(this TextVersionKind value) => value switch
    {
        TextVersionKind.Create => "create",
        TextVersionKind.Replace => "replace",
        TextVersionKind.Insert => "insert",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToWire(this TextVersionAuthor value) => value switch
    {
        TextVersionAuthor.User => "user",
        TextVersionAuthor.Agent => "agent",
        TextVersionAuthor.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
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
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown intent training author."),
    };

    public static string ToWire(this McpCallOutcome value) => value switch
    {
        McpCallOutcome.Success => "success",
        McpCallOutcome.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
