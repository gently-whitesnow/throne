using System.ComponentModel;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Compact response for Intent write tools (create_intent, replace_intent_text,
/// insert_intent_text_after_line). The agent gets back just enough state to chain
/// the next optimistic write; if it needs the full text, it re-reads via get_intent
/// or read_intent_text. This keeps write tool_results well under the wire budget
/// even when Intent.text grows past tens of kilobytes.
/// </summary>
public sealed record McpWriteAck(
    [property: Description("Intent id mutated by the call.")] string IntentId,
    [property: Description("Intent.current_version after the write. Pass as expected_version on the next write.")] int CurrentVersion,
    [property: Description("Always true on success — errors come back as IsError=true tool_results with a typed error code.")] bool Accepted);
