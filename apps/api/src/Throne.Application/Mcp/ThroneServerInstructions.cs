namespace Throne.Application.Mcp;

/// <summary>
/// Mini-router shipped to every MCP client via <c>InitializeResult.instructions</c>
/// (see MCP spec, Lifecycle / InitializeResult). It explains how explicit
/// standalone bundle requests map to <c>get_prompt_bundle</c> without forcing
/// already-contextualized embedded sessions to read a bundle again.
/// </summary>
public static class ThroneServerInstructions
{
    public const string MiniRouter = """
        This is Throne, an MCP server for intents. The working playbook for an intent is not in local files — it comes from get_prompt_bundle.

        When the user asks to read/«прочитай» a bundle for a mode (work, interview, dream, schema_map), call get_prompt_bundle({mode, intent_id}) and follow the text it returns — it is the source of truth. Surface any missing_keys to the user instead of improvising. intent_id comes from the message or active context; for work/interview create one via create_intent if none is given (dream/schema_map run without an intent).

        If the user describes a task without naming a bundle, pick the mode by meaning — execute/continue an intent → work; clarify/shape it → interview; improve instructions from feedback → dream — then read that bundle as above.
        """;
}
