using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Singleton document in the <see cref="MongoCollectionNames.Settings"/> collection
/// (<c>_id = "terminal"</c>, alongside the capabilities singleton). Holds the single
/// operator-controlled terminal setting: the default agent vendor. Model and effort
/// defaults are native to the vendor and never persisted.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class TerminalSettingsDocument
{
    public const string SingletonId = "terminal";

    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("default_vendor")]
    public string DefaultVendor { get; set; } = string.Empty;
}
