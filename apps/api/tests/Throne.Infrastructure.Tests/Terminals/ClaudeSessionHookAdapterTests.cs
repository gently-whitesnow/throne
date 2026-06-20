using System.Text.Json;
using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class ClaudeSessionHookAdapterTests
{
    [Fact(DisplayName = "Пишет per-session settings со всеми четырьмя hook'ами и возвращает --settings <файл>")]
    public async Task Writes_all_hooks_and_returns_flag()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions
        {
            ApiBaseUrl = "http://localhost:5008/",
        });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, skillPackages: [], CancellationToken.None);

        var settingsPath = Path.Combine(root, "throne-session.settings.json");
        args.Should().Equal("--settings", settingsPath);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        foreach (var hookEvent in new[] { "Stop", "UserPromptSubmit", "Notification", "PostToolUse" })
        {
            HookCommand(document, hookEvent).Should().Be(
                $"curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/{hookEvent}?mode=work' " +
                "-H 'Content-Type: application/json' -d @-");
        }

        using var mcp = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, ".mcp.json")));
        var server = mcp.RootElement.GetProperty("mcpServers").GetProperty("throne");
        server.GetProperty("type").GetString().Should().Be("http");
        server.GetProperty("url").GetString().Should().Be("http://localhost:5008/mcp");

        using var projectSettings = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, ".claude", "settings.local.json")));
        projectSettings.RootElement.GetProperty("enabledMcpjsonServers")[0].GetString().Should().Be("throne");
    }

    [Fact(DisplayName = "Notification скоупится matcher'ом permission_prompt; остальные хуки без matcher")]
    public async Task Notification_carries_permission_prompt_matcher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, skillPackages: [], CancellationToken.None);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "throne-session.settings.json")));
        HookMatcher(document, "Notification").Should().Be("permission_prompt");
        HookMatcher(document, "Stop").Should().BeNull();
        HookMatcher(document, "PostToolUse").Should().BeNull();
    }

    [Fact(DisplayName = "Непустой systemPrompt пишется в файл дословно и подаётся через --append-system-prompt-file")]
    public async Task Writes_system_prompt_file_and_references_it()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "RULES\nblock", skillPackages: [], CancellationToken.None);

        var settingsPath = Path.Combine(root, "throne-session.settings.json");
        var systemPromptPath = Path.Combine(root, "throne-session.append-system-prompt.txt");
        args.Should().Equal("--settings", settingsPath, "--append-system-prompt-file", systemPromptPath);
        var written = await File.ReadAllTextAsync(systemPromptPath);
        written.Should().Be("RULES\nblock");
    }

    [Fact(DisplayName = "Пустой systemPrompt не пишет файл и не добавляет флаг")]
    public async Task Blank_system_prompt_writes_no_file()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "   ", skillPackages: [], CancellationToken.None);

        args.Should().NotContain("--append-system-prompt-file");
        File.Exists(Path.Combine(root, "throne-session.append-system-prompt.txt")).Should().BeFalse();
    }

    [Fact(DisplayName = "Spawn-режим попадает в hook-URL как query mode — interview")]
    public async Task Bakes_spawn_mode_into_hook_url()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Interview, systemPrompt: null, skillPackages: [], CancellationToken.None);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "throne-session.settings.json")));
        HookCommand(document, "UserPromptSubmit").Should().Contain("/hooks/UserPromptSubmit?mode=interview'");
    }

    [Fact(DisplayName = "Review-сессия запекает общий artifact writer и Claude skill-hint")]
    public async Task Review_writes_artifact_script_and_skill()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            skillPackages: [new ReviewArtifactSessionSkillPackage(new ReviewArtifactWriteTarget("binding-1", 42))],
            CancellationToken.None);

        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-pr-artifact-write"));
        script.Should().Contain("BINDING_ID='binding-1'");
        script.Should().Contain("ARTIFACT_TYPE='review_recommendation'");
        script.Should().Contain("/api/v1/repositories/${BINDING_ID}/artifacts/${ARTIFACT_TYPE}");

        var skill = await File.ReadAllTextAsync(
            Path.Combine(root, ".claude", "skills", "throne-review-artifact", "SKILL.md"));
        skill.Should().Contain("bin/throne-pr-artifact-write");
        skill.Should().Contain("send-comments");
    }

    [Fact(DisplayName = "Interview-сессия пишет intent-ops script + 2 Claude skills без Throne MCP config")]
    public async Task Interview_writes_intent_operations_script_and_skills()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        await sut.PrepareSpawnArgsAsync(
            "intent-1",
            root,
            TerminalRunModes.Interview,
            systemPrompt: null,
            skillPackages: [new IntentOperationsSessionSkillPackage("intent-1")],
            CancellationToken.None);

        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-intent"));
        script.Should().Contain("INTENT_ID='intent-1'");
        script.Should().Contain("API_BASE=\"${THRONE_API_BASE:-http://localhost:5008}\"");
        script.Should().Contain("/api/v1/intents/${INTENT_ID}/replace-text");
        script.Should().Contain("\"expected_version\": int(sys.argv[1])");

        var textSkill = await File.ReadAllTextAsync(
            Path.Combine(root, ".claude", "skills", "throne-intent-text", "SKILL.md"));
        textSkill.Should().Contain("replace-text --old-file");

        var childSkill = await File.ReadAllTextAsync(
            Path.Combine(root, ".claude", "skills", "throne-intent-decompose", "SKILL.md"));
        childSkill.Should().Contain("create --text-file");
        childSkill.Should().Contain("link \"$child_id\"");

        File.Exists(Path.Combine(root, ".mcp.json")).Should().BeFalse();
        File.Exists(Path.Combine(root, ".claude", "settings.local.json")).Should().BeFalse();
    }

    [Theory(DisplayName = "IsTuiReady распознаёт композёр Claude по `❯` промпту и игнорирует пустой/только-сплеш экран")]
    [InlineData("", false)]
    [InlineData("╭─── Claude Code v2.1.116 ───╮\n│  Welcome back Alexander!  │\n╰────────────────────────────╯", false)]
    [InlineData("─────────────\n❯ Try \"how does <filepath> work?\"\n─────────────", true)]
    public void Is_tui_ready_matches_composer_input_row(string snapshot, bool expected)
    {
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        sut.IsTuiReady(snapshot).Should().Be(expected);
    }

    private static string? HookCommand(JsonDocument document, string hookEvent) =>
        document.RootElement
            .GetProperty("hooks")
            .GetProperty(hookEvent)[0]
            .GetProperty("hooks")[0]
            .GetProperty("command")
            .GetString();

    private static string? HookMatcher(JsonDocument document, string hookEvent) =>
        document.RootElement
            .GetProperty("hooks")
            .GetProperty(hookEvent)[0]
            .TryGetProperty("matcher", out var matcher)
            ? matcher.GetString()
            : null;
}
