using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;

namespace Throne.MigrateMongoSqlite;

internal static class BsonJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    public static string Serialize(BsonValue value) =>
        ToJsonNode(value)?.ToJsonString(Options) ?? "null";

    private static JsonNode? ToJsonNode(BsonValue value) =>
        value.BsonType switch
        {
            BsonType.Null or BsonType.Undefined => null,
            BsonType.Boolean => JsonValue.Create(value.AsBoolean),
            BsonType.String => JsonValue.Create(value.AsString),
            BsonType.Int32 => JsonValue.Create(value.AsInt32),
            BsonType.Int64 => JsonValue.Create(value.AsInt64),
            BsonType.Double => JsonValue.Create(value.AsDouble),
            BsonType.Decimal128 => JsonValue.Create(value.AsDecimal128.ToString()),
            BsonType.DateTime => JsonValue.Create(BsonFields.DateTimeOffset(value).ToString("O")),
            BsonType.ObjectId => JsonValue.Create(value.AsObjectId.ToString()),
            BsonType.Binary => JsonValue.Create(Convert.ToBase64String(value.AsByteArray)),
            BsonType.Array => ToJsonArray(value.AsBsonArray),
            BsonType.Document => ToJsonObject(value.AsBsonDocument),
            _ => JsonValue.Create(value.ToString()),
        };

    private static JsonArray ToJsonArray(BsonArray array)
    {
        var json = new JsonArray();
        foreach (var item in array)
        {
            json.Add(ToJsonNode(item));
        }
        return json;
    }

    private static JsonObject ToJsonObject(BsonDocument document)
    {
        var json = new JsonObject();
        foreach (var element in document)
        {
            json[element.Name] = ToJsonNode(element.Value);
        }
        return json;
    }
}
