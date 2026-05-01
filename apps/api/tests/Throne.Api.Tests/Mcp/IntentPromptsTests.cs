using FluentAssertions;
using Throne.Api.Mcp.Prompts;

namespace Throne.Api.Tests.Mcp;

public class IntentPromptsTests
{
    private static readonly IntentPrompts Prompts = new();

    public static IEnumerable<object[]> AllPrompts() =>
    [
        ["tinterview", "interview"],
        ["twork", "light_work"],
        ["tnew", "new_project"],
        ["treview", "light_work"],
    ];

    [Theory(DisplayName = "Каждый prompt содержит общие правила и упоминает свой mode")]
    [MemberData(nameof(AllPrompts))]
    public void Prompt_contains_shared_rules_and_mode(string promptName, string expectedMode)
    {
        var rendered = Invoke(promptName, intentId: null, text: null);

        rendered.Should().Contain("Active Intent resolution");
        rendered.Should().Contain("expected_version");
        rendered.Should().Contain("get_instruction_bundle");
        rendered.Should().Contain($"mode = `{expectedMode}`");
    }

    [Theory(DisplayName = "Если передан intent_id — он попадает в контекст вызова")]
    [MemberData(nameof(AllPrompts))]
    public void Prompt_substitutes_intent_id(string promptName, string _)
    {
        var rendered = Invoke(promptName, intentId: "intent_42", text: null);

        rendered.Should().Contain("intent_42");
        rendered.Should().Contain("Active Intent для сессии");
    }

    [Theory(DisplayName = "Если передан только text — Intent создаётся из него по правилу 3 active-resolution")]
    [MemberData(nameof(AllPrompts))]
    public void Prompt_substitutes_text(string promptName, string _)
    {
        var rendered = Invoke(promptName, intentId: null, text: "хочу сделать MCP-хранилище intent'ов");

        rendered.Should().Contain("хочу сделать MCP-хранилище");
        rendered.Should().Contain("create_intent");
    }

    [Fact(DisplayName = "tinterview playbook требует ровно один вопрос за шаг и add_intent_qa")]
    public void Tinterview_playbook_enforces_single_question_and_qa()
    {
        var rendered = Prompts.TInterview(null, null);
        rendered.Should().Contain("один");
        rendered.Should().Contain("add_intent_qa");
    }

    [Fact(DisplayName = "treview playbook фиксирует add_intent_review и не сохраняет результат work внутри Intent")]
    public void Treview_playbook_calls_review_tool_and_keeps_work_outside_intent()
    {
        var rendered = Prompts.TReview(null, "не надо было создавать сервис");
        rendered.Should().Contain("add_intent_review");
        rendered.Should().Contain("не сохраняй");
    }

    [Fact(DisplayName = "Имена tools в playbook'ах совпадают с существующими [McpServerTool] в IntentTools")]
    public void Playbooks_reference_only_existing_tools()
    {
        var allRendered = string.Join('\n',
            Prompts.TInterview(null, null),
            Prompts.TWork(null, null),
            Prompts.TNew(null, null),
            Prompts.TReview(null, null));

        var existingTools = new[]
        {
            "create_intent",
            "get_intent",
            "read_intent_text",
            "search_intent_text",
            "replace_intent_text",
            "insert_intent_text_after_line",
            "add_intent_qa",
            "add_intent_review",
            "get_instruction_bundle",
        };

        foreach (var tool in existingTools.Where(t => allRendered.Contains(t, StringComparison.Ordinal)))
        {
            tool.Should().BeOneOf(existingTools);
        }

        allRendered.Should().NotContain("create_instruction");
        allRendered.Should().NotContain("replace_instruction_text");
        allRendered.Should().NotContain("list_intents");
    }

    [Fact(DisplayName = "Все 4 имени prompt'ов экспортированы через PromptNames.All")]
    public void Prompt_names_are_exported()
    {
        PromptNames.All.Should().BeEquivalentTo([
            PromptNames.TInterview,
            PromptNames.TWork,
            PromptNames.TNew,
            PromptNames.TReview,
        ]);
    }

    private static string Invoke(string promptName, string? intentId, string? text) => promptName switch
    {
        "tinterview" => Prompts.TInterview(intentId, text),
        "twork" => Prompts.TWork(intentId, text),
        "tnew" => Prompts.TNew(intentId, text),
        "treview" => Prompts.TReview(intentId, text),
        _ => throw new ArgumentException($"Unknown prompt: {promptName}"),
    };
}
