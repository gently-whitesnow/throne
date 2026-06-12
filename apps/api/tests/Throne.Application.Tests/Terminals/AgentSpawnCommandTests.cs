using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class AgentSpawnCommandTests
{
    [Fact(DisplayName = "claude: --model/--effort + позиционная задача")]
    public void Claude_base_args_and_positional_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(options, userPrompt: "TASK");

        invocation.Command.Should().Be("claude");
        invocation.Arguments.Should().Equal("--model", "opus", "--effort", "high", "TASK");
    }

    [Fact(DisplayName = "prepared-аргументы адаптера вставляются между base-флагами и позиционной задачей")]
    public void Prepared_args_between_base_and_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(
            options,
            userPrompt: "TASK",
            preparedArgs: ["--settings", "/tmp/settings.json", "--append-system-prompt-file", "/tmp/sp.txt"]);

        invocation.Arguments.Should().Equal(
            "--model", "opus", "--effort", "high",
            "--settings", "/tmp/settings.json",
            "--append-system-prompt-file", "/tmp/sp.txt",
            "TASK");
    }

    [Fact(DisplayName = "codex: -m, -c model_reasoning_effort, bypass + позиционная задача")]
    public void Codex_base_args_and_positional_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.5", "medium");

        var invocation = AgentSpawnCommand.Build(options, userPrompt: "TASK");

        invocation.Command.Should().Be("codex");
        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.5",
            "-c", "model_reasoning_effort=medium",
            "--dangerously-bypass-approvals-and-sandbox",
            "TASK");
    }

    [Fact(DisplayName = "пустая задача не добавляет позиционный промпт — агент стартует без него")]
    public void Blank_user_prompt_drops_positional()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.4", "xhigh");

        var invocation = AgentSpawnCommand.Build(options, userPrompt: null);

        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.4",
            "-c", "model_reasoning_effort=xhigh",
            "--dangerously-bypass-approvals-and-sandbox");
    }
}
