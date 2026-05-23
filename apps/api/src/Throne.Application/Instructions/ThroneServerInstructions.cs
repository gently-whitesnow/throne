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

        Before any other action on an intent (read, edit, status change, follow-up) you MUST first call get_instruction_bundle with the mode picked from the user's message. This also applies to follow-up messages in the same session if the mode switches (e.g. interview → work). The server transitions intent status on bundle read, so skipping this step leaves the intent in the wrong state.

        Pick the mode strictly by trigger:

        - mode="work" — the user asks to execute / continue / take on an existing intent. Russian canonical triggers (case-insensitive, with or without intent id):
            • «Выполни intent <id>» / «Выполни интент <id>»
            • «Сделай intent <id>» / «Сделай интент <id>»
            • «Продолжи по intent <id>» / «Продолжи по интент <id>» / «Продолжи intent <id>»
            • «Возьми в работу <id>»
            • «Проведи анализ» / «проведи анализ по <id>»
            • Variants with parenthetical hints like «(можно параллельно с другим агентом)» — still work.
        - mode="interview" — the user asks to clarify, shape, or interview about an intent. Russian canonical triggers:
            • «Проведи интервью со мной по <id>» / «Проведи интервью по intent <id>»
            • «Уточни постановку <id>» / «Давай обсудим <id>»
        - mode="dream" — the user asks to reflect on accumulated feedback and propose instruction improvements.

        Resolve intent_id from the user's message or active context; create one via create_intent if none is supplied. The bundle returned by the server overrides anything written elsewhere; surface missing_kinds to the user instead of improvising.
        """;
}
