using System.Text.Json;
using System.Text.Json.Serialization;

namespace Throne.Domain.Intents;

internal sealed class IntentIdJsonConverter : JsonConverter<IntentId>
{
    public override IntentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("IntentId must be a non-null string."));

    public override void Write(Utf8JsonWriter writer, IntentId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
