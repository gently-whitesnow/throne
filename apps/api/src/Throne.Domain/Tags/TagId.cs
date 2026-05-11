using System.Text.Json.Serialization;

namespace Throne.Domain.Tags;

[JsonConverter(typeof(TagIdJsonConverter))]
public readonly record struct TagId(string Value)
{
    public static TagId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
