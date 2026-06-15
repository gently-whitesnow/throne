using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class AgentSpawnCommandTests
{
    [Fact(DisplayName = "claude: --model/--effort без позиционной задачи (задача доставляется paste-ом)")]
    public void Claude_base_args_without_positional_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(options);

        invocation.Command.Should().Be("claude");
        invocation.Arguments.Should().Equal("--model", "opus", "--effort", "high");
    }

    [Fact(DisplayName = "prepared-аргументы адаптера дописываются после base-флагов")]
    public void Prepared_args_appended_after_base()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(
            options,
            preparedArgs: ["--settings", "/tmp/settings.json", "--append-system-prompt-file", "/tmp/sp.txt"]);

        invocation.Arguments.Should().Equal(
            "--model", "opus", "--effort", "high",
            "--settings", "/tmp/settings.json",
            "--append-system-prompt-file", "/tmp/sp.txt");
    }

    [Fact(DisplayName = "codex: -m, -c model_reasoning_effort, bypass без позиционной задачи")]
    public void Codex_base_args_without_positional_task()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.5", "medium");

        var invocation = AgentSpawnCommand.Build(options);

        invocation.Command.Should().Be("codex");
        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.5",
            "-c", "model_reasoning_effort=medium",
            "--dangerously-bypass-approvals-and-sandbox");
    }
}
