using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class IntentSkillModeSelectionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("mode_selections")]
    public List<IntentSkillModeSelectionEntryDocument> ModeSelections { get; set; } = [];
}

[BsonIgnoreExtraElements]
internal sealed class IntentSkillModeSelectionEntryDocument
{
    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("selected_skill_ids")]
    public List<string> SelectedSkillIds { get; set; } = [];
}
