using System.Text.Json.Serialization;

namespace Throne.Domain.Intents;

[JsonConverter(typeof(IntentIdJsonConverter))]
public readonly record struct IntentId(string Value)
{
    public static IntentId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
