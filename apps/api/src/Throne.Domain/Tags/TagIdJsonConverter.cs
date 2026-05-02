using System.Text.Json;
using System.Text.Json.Serialization;

namespace Throne.Domain.Tags;

internal sealed class TagIdJsonConverter : JsonConverter<TagId>
{
    public override TagId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("TagId must be a non-null string."));

    public override void Write(Utf8JsonWriter writer, TagId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
