namespace Throne.Application.Mcp;

/// <summary>
/// Mini-router shipped to every MCP client via <c>InitializeResult.instructions</c>
/// (see MCP spec, Lifecycle / InitializeResult). Triggers <c>get_prompt_bundle</c>
/// only on an explicit user request, so already-contextualized embedded sessions
/// are not pushed into a redundant bundle read by a generic task description.
/// </summary>
public static class ThroneServerInstructions
{
    public const string MiniRouter = """
        This is Throne, an MCP server for intents. The working playbook for an intent is not in local files — it comes from get_prompt_bundle.

        When the user asks to read/«прочитай» a bundle for a mode (work, interview, review, dream, schema_map), call get_prompt_bundle({mode, intent_id}) and follow the text it returns — it is the source of truth. Surface any missing_keys to the user instead of improvising. intent_id comes from the message or active context; for work/interview/review create one via create_intent if none is given (dream/schema_map run without an intent).

        Do not call get_prompt_bundle on your own initiative when the user merely describes a task without asking to read a bundle — wait for an explicit request.
        """;
}
