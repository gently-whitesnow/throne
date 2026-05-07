namespace Throne.Application.Instructions;

/// <summary>
/// Mini-router shipped to every MCP client via <c>InitializeResult.instructions</c>
/// (see MCP spec, Lifecycle / InitializeResult). It tells the agent that on the
/// first contact with Throne it must pull mode-specific instructions through
/// <c>get_instruction_bundle</c> rather than guess from local skill files —
/// which no longer exist after ADR-0007 was superseded.
/// </summary>
public static class ThroneServerInstructions
{
    public const string MiniRouter = """
        This is Throne. The live playbook for working with Throne intents lives on this MCP server, not in local skill files.

        On the first user request, choose the bundle mode by intent and call get_instruction_bundle, then follow the returned instructions strictly:

        - If the user asks to clarify or shape an idea/task → mode="interview".
        - If the user asks to do work in the current repository on an existing intent → mode="work".
        - If the user gives feedback on a previous work pass and asks to continue → mode="fix" (record their feedback via add_intent_review first).
        - If the user asks to reflect on accumulated feedback and propose instruction improvements → mode="dream".
        - If the user asks to send chat history to Throne for training → mode="transfer".

        Resolve intent_id from the user's message or active context; create one via create_intent if none is supplied. The bundle returned by the server overrides anything written elsewhere; surface missing_kinds to the user instead of improvising.
        """;
}
