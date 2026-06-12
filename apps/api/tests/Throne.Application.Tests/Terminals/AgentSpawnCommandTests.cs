using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class AgentSpawnCommandTests
{
    [Fact(DisplayName = "claude: команда claude с флагами --model/--effort и позиционным промптом")]
    public void Claude_builds_model_effort_flags_with_prompt()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(options, "Прочитай бандл work и выполни интент x", isFree: false);

        invocation.Command.Should().Be("claude");
        invocation.Arguments.Should().Equal(
            "--model", "opus", "--effort", "high", "Прочитай бандл work и выполни интент x");
    }

    [Fact(DisplayName = "claude: settings-файл передаётся через --settings до позиционного промпта")]
    public void Claude_adds_settings_flag_before_prompt()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "opus", "high");

        var invocation = AgentSpawnCommand.Build(options, "промпт", isFree: false, "/tmp/settings.json");

        invocation.Arguments.Should().Equal(
            "--model", "opus", "--effort", "high", "--settings", "/tmp/settings.json", "промпт");
    }

    [Fact(DisplayName = "codex: -m, -c model_reasoning_effort и автономный bypass-флаг без shell-кавычек")]
    public void Codex_builds_model_reasoning_effort_and_autonomous_bypass()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.5", "medium");

        var invocation = AgentSpawnCommand.Build(options, "промпт", isFree: false);

        invocation.Command.Should().Be("codex");
        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.5",
            "-c", "model_reasoning_effort=medium",
            "--dangerously-bypass-approvals-and-sandbox",
            "промпт");
    }

    [Fact(DisplayName = "codex: settings-файл Claude игнорируется")]
    public void Codex_ignores_claude_settings()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.5", "medium");

        var invocation = AgentSpawnCommand.Build(options, "промпт", isFree: false, "/tmp/settings.json");

        invocation.Arguments.Should().NotContain("--settings");
        invocation.Arguments.Should().NotContain("/tmp/settings.json");
    }

    [Fact(DisplayName = "free-режим не добавляет позиционный промпт, но сохраняет флаги")]
    public void Free_mode_keeps_flags_drops_positional_prompt()
    {
        var options = new TerminalLaunchOptions(TerminalAgentCatalog.VendorCodex, "gpt-5.4", "xhigh");

        var invocation = AgentSpawnCommand.Build(options, "промпт", isFree: true);

        invocation.Arguments.Should().Equal(
            "-m", "gpt-5.4",
            "-c", "model_reasoning_effort=xhigh",
            "--dangerously-bypass-approvals-and-sandbox");
    }
}
