using MongoDB.Bson;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Safe, whitelisted projection of <see cref="Documents.McpCallLogDocument.Arguments"/> for
/// query_mcp_call_log responses. Tools that are not in the whitelist contribute null
/// to keep raw, potentially-sensitive payloads out of read responses.
/// See ADR-0004 §argument_summary.
/// </summary>
internal static class McpArgumentSummaries
{
    private const int PreviewMaxChars = 80;

    public static IReadOnlyDictionary<string, object?>? Build(string toolName, BsonDocument arguments) =>
        toolName switch
        {
            "replace_intent_text" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_version"] = TryInt(arguments, "expected_version"),
                ["old_text_preview"] = Preview(arguments, "old_text"),
                ["new_text_preview"] = Preview(arguments, "new_text"),
            },
            "insert_intent_text_after_line" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_version"] = TryInt(arguments, "expected_version"),
                ["after_line"] = TryInt(arguments, "after_line"),
                ["insert_text_preview"] = Preview(arguments, "insert_text"),
            },
            "get_instruction_bundle" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = TryString(arguments, "mode"),
                ["intent_id"] = TryString(arguments, "intent_id"),
            },
            "create_intent" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["text_preview"] = Preview(arguments, "text"),
                ["tags"] = TryStringArray(arguments, "tags"),
            },
            "propose_instruction_patch" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["target_kind"] = TryString(arguments, "target_kind"),
                ["base_instruction_version"] = TryInt(arguments, "base_instruction_version"),
                ["evidence_card_count"] = TryStringArray(arguments, "evidence_card_ids")?.Count,
                ["rationale_preview"] = Preview(arguments, "rationale"),
                ["patch_text_preview"] = Preview(arguments, "patch_text"),
                ["idempotency_key"] = TryString(arguments, "idempotency_key"),
            },
            "dismiss_insight_card" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["card_id"] = TryString(arguments, "card_id"),
                ["comment_preview"] = Preview(arguments, "comment"),
            },
            "read_chat_span" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["upload_id"] = TryString(arguments, "upload_id"),
                ["vendor"] = TryString(arguments, "vendor"),
                ["conversation_id"] = TryString(arguments, "conversation_id"),
                ["start_index"] = TryInt(arguments, "start_index"),
                ["end_index"] = TryInt(arguments, "end_index"),
            },
            _ => null,
        };

    private static string? TryString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var v) && v.BsonType == BsonType.String ? v.AsString : null;

    private static int? TryInt(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var v))
        {
            return null;
        }
        return v.BsonType switch
        {
            BsonType.Int32 => v.AsInt32,
            BsonType.Int64 => (int)v.AsInt64,
            BsonType.Double => (int)v.AsDouble,
            _ => null,
        };
    }

    private static string? Preview(BsonDocument doc, string field)
    {
        var s = TryString(doc, field);
        if (s is null)
        {
            return null;
        }
        return s.Length <= PreviewMaxChars ? s : s[..PreviewMaxChars];
    }

    private static List<string>? TryStringArray(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var v) || v.BsonType != BsonType.Array)
        {
            return null;
        }
        var result = new List<string>();
        foreach (var item in v.AsBsonArray)
        {
            if (item.BsonType == BsonType.String)
            {
                result.Add(item.AsString);
            }
        }
        return result;
    }
}
