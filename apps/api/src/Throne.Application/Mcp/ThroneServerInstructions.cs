namespace Throne.Application.Mcp;

/// <summary>
/// Instructions shipped to every standalone MCP client via <c>InitializeResult.instructions</c>
/// (see MCP spec, Lifecycle / InitializeResult). Throne is a knowledge base of intents for
/// external agents: they read and write <c>Intent.text</c> and may change status, but the
/// working playbook is not delivered over MCP — full intent execution lives in the embedded
/// contour (see ADR-0034).
/// </summary>
public static class ThroneServerInstructions
{
    public const string MiniRouter = """
        This is Throne, an MCP server that acts as a knowledge base of intents. Each intent has a canonical Intent.text plus status, tags, attachments and a link graph.

        Use the intent-management tools to read and write that knowledge: get_intent / read_intent_text to read, replace_intent_text / insert_intent_text_after_line to edit Intent.text, list_intents / search_intent_text to discover, and the link/tag tools for relationships. The active intent_id comes from the message or context.

        Change an intent's status with set_intent_status only when the user explicitly asks for it. Create a new intent with create_intent only on an explicit request — there is no automatic status transition and no implicit intent creation.

        Throne does not ship a working playbook over MCP: follow the user's own instructions for how to carry out a task.
        """;
}
