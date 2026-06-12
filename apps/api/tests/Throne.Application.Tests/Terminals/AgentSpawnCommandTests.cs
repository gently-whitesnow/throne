using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class AgentSpawnCommandTests
{
    [Fact(DisplayName = "claude: --model/--effort, правила через --append-system-prompt, задача позиционным аргументом")]
    public void Claude_appends_system_prompt_and_positional_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(options, systemPrompt: "RULES", userPrompt: "TASK");

        invocation.Command.Should().Be("claude");
        invocation.Arguments.Should().Equal(
            "--model", "opus", "--effort", "high", "--append-system-prompt", "RULES", "TASK");
    }

    [Fact(DisplayName = "claude: hook-аргументы между system-флагами и позиционной задачей")]
    public void Claude_hook_args_between_system_and_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(
            options, systemPrompt: "RULES", userPrompt: "TASK", ["--settings", "/tmp/settings.json"]);

        invocation.Arguments.Should().Equal(
            "--model", "opus", "--effort", "high",
            "--append-system-prompt", "RULES",
            "--settings", "/tmp/settings.json",
            "TASK");
    }

    [Fact(DisplayName = "codex: -m, -c model_reasoning_effort, bypass и developer_instructions TOML-строкой, задача позиционно")]
    public void Codex_injects_developer_instructions_and_positional_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.5", "medium");

        var invocation = AgentSpawnCommand.Build(options, systemPrompt: "RULES", userPrompt: "TASK");

        invocation.Command.Should().Be("codex");
        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.5",
            "-c", "model_reasoning_effort=medium",
            "--dangerously-bypass-approvals-and-sandbox",
            "-c", "developer_instructions=\"RULES\"",
            "TASK");
    }

    [Fact(DisplayName = "codex: developer_instructions экранирует кавычки и переводы строк в TOML basic string")]
    public void Codex_escapes_developer_instructions()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.5", "medium");

        var invocation = AgentSpawnCommand.Build(options, systemPrompt: "say \"hi\"\nline2", userPrompt: null);

        invocation.Arguments.Should().Contain("developer_instructions=\"say \\\"hi\\\"\\nline2\"");
    }

    [Fact(DisplayName = "пустые правила не добавляют system-флаги")]
    public void Blank_system_prompt_drops_system_flags()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(options, systemPrompt: "  ", userPrompt: "TASK");

        invocation.Arguments.Should().Equal("--model", "opus", "--effort", "high", "TASK");
    }

    [Fact(DisplayName = "пустая задача не добавляет позиционный промпт — агент стартует без него")]
    public void Blank_user_prompt_drops_positional()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.4", "xhigh");

        var invocation = AgentSpawnCommand.Build(options, systemPrompt: null, userPrompt: null);

        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.4",
            "-c", "model_reasoning_effort=xhigh",
            "--dangerously-bypass-approvals-and-sandbox");
    }
}
