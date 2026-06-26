using System.Globalization;
using MongoDB.Bson;

namespace Throne.MigrateMongoSqlite;

internal static class BsonFields
{
    public static string Id(BsonDocument document) => String(document, "_id");

    public static string String(BsonDocument document, string name, string defaultValue = "") =>
        ValueOrNull(document, name) is { } value ? String(value, defaultValue) : defaultValue;

    public static string RequiredStatus(BsonDocument document, string name) =>
        NonBlankString(document, name, "draft");

    public static string NonBlankString(BsonDocument document, string name, string defaultValue)
    {
        var value = String(document, name, defaultValue);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public static string? NullableString(BsonDocument document, string name) =>
        ValueOrNull(document, name) is { } value ? String(value, string.Empty) : null;

    public static int Int32(BsonDocument document, string name, int defaultValue = 0) =>
        ValueOrNull(document, name) is { } value ? ToInt32(value, defaultValue) : defaultValue;

    public static int PositiveInt32(BsonDocument document, string name, int defaultValue = 1)
    {
        var value = Int32(document, name, defaultValue);
        return value < 1 ? defaultValue : value;
    }

    public static int? NullableInt32(BsonDocument document, string name) =>
        ValueOrNull(document, name) is { } value ? ToInt32(value, 0) : null;

    public static long Int64(BsonDocument document, string name, long defaultValue = 0L) =>
        ValueOrNull(document, name) is { } value ? ToInt64(value, defaultValue) : defaultValue;

    public static bool Bool(BsonDocument document, string name, bool defaultValue = false) =>
        ValueOrNull(document, name) is { } value ? ToBool(value, defaultValue) : defaultValue;

    public static string DateTimeText(BsonDocument document, string name) =>
        DateTimeOffset(document, name).ToString("O", CultureInfo.InvariantCulture);

    public static string? NullableDateTimeText(BsonDocument document, string name) =>
        ValueOrNull(document, name) is { } value
            ? DateTimeOffset(value).ToString("O", CultureInfo.InvariantCulture)
            : null;

    public static string JsonOrDefault(BsonDocument document, string name, string defaultJson) =>
        ValueOrNull(document, name) is { } value ? BsonJson.Serialize(value) : defaultJson;

    public static string? JsonOrNull(BsonDocument document, string name) =>
        ValueOrNull(document, name) is { } value ? BsonJson.Serialize(value) : null;

    public static string EffectiveHost(BsonDocument document)
    {
        var provider = String(document, "provider");
        var host = NullableString(document, "host");
        if (string.Equals(provider, "github", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(host))
        {
            return "github.com";
        }
        return host ?? string.Empty;
    }

    public static System.DateTimeOffset DateTimeOffset(BsonDocument document, string name) =>
        ValueOrNull(document, name) is { } value ? DateTimeOffset(value) : System.DateTimeOffset.MinValue;

    public static System.DateTimeOffset DateTimeOffset(BsonValue value) =>
        value.BsonType switch
        {
            BsonType.DateTime => new System.DateTimeOffset(
                DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc)),
            BsonType.String when System.DateTimeOffset.TryParse(
                value.AsString,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) => parsed,
            _ => System.DateTimeOffset.MinValue,
        };

    public static BsonValue? ValueOrNull(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value)
            || value.IsBsonNull
            || value.BsonType == BsonType.Undefined)
        {
            return null;
        }
        return value;
    }

    private static string String(BsonValue value, string defaultValue) =>
        value.BsonType switch
        {
            BsonType.String => value.AsString,
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.Int32 => value.AsInt32.ToString(CultureInfo.InvariantCulture),
            BsonType.Int64 => value.AsInt64.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? defaultValue,
        };

    private static int ToInt32(BsonValue value, int defaultValue) =>
        value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => checked((int)value.AsInt64),
            BsonType.Double => checked((int)value.AsDouble),
            BsonType.String when int.TryParse(value.AsString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => defaultValue,
        };

    private static long ToInt64(BsonValue value, long defaultValue) =>
        value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => checked((long)value.AsDouble),
            BsonType.String when long.TryParse(value.AsString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => defaultValue,
        };

    private static bool ToBool(BsonValue value, bool defaultValue) =>
        value.BsonType switch
        {
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32 != 0,
            BsonType.Int64 => value.AsInt64 != 0L,
            BsonType.String when bool.TryParse(value.AsString, out var parsed) => parsed,
            _ => defaultValue,
        };
}
