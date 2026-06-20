using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Throne.Infrastructure.Terminals;

// Shape mirrors https://opencode.ai/config.json — `$schema`, `baseURL` and friends must
// serialize verbatim, so each record property carries an explicit JsonPropertyName instead
// of relying on the global naming policy. Kept internal: only the adapter ever materialises
// these records and OpenCode reads the JSON, not us.
internal sealed record OpencodeConfigDocument(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("provider")] IReadOnlyDictionary<string, OpencodeConfigProvider> Provider,
    [property: JsonPropertyName("instructions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? Instructions,
    [property: JsonPropertyName("mcp"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyDictionary<string, JsonNode>? Mcp);

internal sealed record OpencodeConfigProvider(
    [property: JsonPropertyName("npm")] string Npm,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("options")] OpencodeConfigProviderOptions Options,
    [property: JsonPropertyName("models")] IReadOnlyDictionary<string, OpencodeConfigModel> Models);

internal sealed record OpencodeConfigProviderOptions(
    [property: JsonPropertyName("baseURL")] string BaseURL);

internal sealed record OpencodeConfigModel(
    [property: JsonPropertyName("name")] string Name);
