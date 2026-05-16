using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp;

internal static class McpResultSummarizer
{
    private static readonly JsonSerializerOptions SummaryJsonOptions = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, object?>? Summarize(string toolName, CallToolResult result)
    {
        var structured = result.StructuredContent;
        if (structured is null)
        {
            return null;
        }

        var json = structured.Value.GetRawText();
        return toolName == "get_instruction_bundle"
            ? SummarizeInstructionBundle(json)
            : DeserializeAsDictionary(json);
    }

    private static Dictionary<string, object?>? DeserializeAsDictionary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, SummaryJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, object?>? SummarizeInstructionBundle(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new Dictionary<string, object?>
            {
                ["intent_id"] = JsonProbe.String(root, "intent_id"),
                ["mode"] = JsonProbe.String(root, "mode"),
                ["instructions"] = ReadInstructionRefs(root),
                ["missing_kinds"] = JsonProbe.StringArray(root, "missing_kinds"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<Dictionary<string, object?>> ReadInstructionRefs(JsonElement root)
    {
        var refs = new List<Dictionary<string, object?>>();
        var items = JsonProbe.Array(root, "instructions");
        foreach (var item in items)
        {
            refs.Add(new Dictionary<string, object?>
            {
                ["kind"] = JsonProbe.String(item, "kind"),
                ["instruction_id"] = JsonProbe.String(item, "instruction_id"),
                ["version"] = JsonProbe.Int(item, "current_version") ?? JsonProbe.Int(item, "version"),
            });
        }
        return refs;
    }
}
