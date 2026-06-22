using System.Text.Json;
using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Domain.Repositories;
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
        }, new SessionSkillMaterializer());

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
    }

    [Fact(DisplayName = "Notification скоупится matcher'ом permission_prompt; остальные хуки без matcher")]
    public async Task Notification_carries_permission_prompt_matcher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = NewAdapter();

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
        var sut = NewAdapter();

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
        var sut = NewAdapter();

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "   ", skillPackages: [], CancellationToken.None);

        args.Should().NotContain("--append-system-prompt-file");
        File.Exists(Path.Combine(root, "throne-session.append-system-prompt.txt")).Should().BeFalse();
    }

    [Fact(DisplayName = "Spawn-режим попадает в hook-URL как query mode — interview")]
    public async Task Bakes_spawn_mode_into_hook_url()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = NewAdapter();

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Interview, systemPrompt: null, skillPackages: [], CancellationToken.None);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "throne-session.settings.json")));
        HookCommand(document, "UserPromptSubmit").Should().Contain("/hooks/UserPromptSubmit?mode=interview'");
    }

    [Fact(DisplayName = "Review-сессия стейджит artifact writer, canonical skill и Claude pointer")]
    public async Task Review_writes_artifact_script_and_skill()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = NewAdapter();

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            skillPackages: [new ReviewSessionSkillPackage(ReviewTarget())],
            CancellationToken.None);

        var scriptPath = Path.Combine(root, "skills", "review", "bin", "throne-review");
        var script = await File.ReadAllTextAsync(scriptPath);
        script.Should().NotContain("binding-1");
        script.Should().Contain("THRONE_REPOSITORY_BINDING_ID");
        script.Should().Contain("ARTIFACT_TYPE=");
        script.Should().Contain("review_recommendation");
        script.Should().Contain("/api/v1/repositories/${binding_id}/artifacts/${ARTIFACT_TYPE}");
        AssertExecutable(scriptPath);

        var canonical = await File.ReadAllTextAsync(Path.Combine(root, "skills", "review", "SKILL.md"));
        canonical.Should().Contain("skills/review/bin/throne-review");
        canonical.Should().Contain("review_recommendation");

        var pointer = await File.ReadAllTextAsync(
            Path.Combine(root, ".claude", "skills", "review", "SKILL.md"));
        pointer.Should().Contain("skills/review/SKILL.md");
        pointer.Should().Contain("skills/review/bin/");
        pointer.Should().NotContain("Payload shape");
    }

    private static ReviewArtifactWriteTarget ReviewTarget() =>
        new("binding-1", new RepoCoordinate(GitProviderNames.GitHub, "octo", "repo"));

    [Fact(DisplayName = "Interview-сессия стейджит статический intent script + Claude skill")]
    public async Task Interview_writes_intent_operations_script_and_skills()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = NewAdapter();

        await sut.PrepareSpawnArgsAsync(
            "intent-1",
            root,
            TerminalRunModes.Interview,
            systemPrompt: null,
            skillPackages: [new IntentSessionSkillPackage()],
            CancellationToken.None);

        var scriptPath = Path.Combine(root, "skills", "intent", "bin", "throne-intent");
        // No UTF-8 BOM: a BOM before `#!` breaks the shebang (ENOEXEC → /bin/sh fallback).
        var scriptBytes = await File.ReadAllBytesAsync(scriptPath);
        scriptBytes.Take(3).Should().NotEqual([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
        scriptBytes.Take(2).Should().Equal([(byte)'#', (byte)'!']);

        var script = await File.ReadAllTextAsync(scriptPath);
        script.Should().NotContain("intent-1");
        script.Should().Contain("INTENT_ID=\"${THRONE_INTENT_ID:-}\"");
        script.Should().Contain("API_BASE=\"${THRONE_API_BASE:-http://localhost:5008}\"");
        script.Should().Contain("/api/v1/intents/${INTENT_ID}/replace-text");
        script.Should().Contain("\"expected_version\": int(sys.argv[1])");

        var canonical = await File.ReadAllTextAsync(Path.Combine(root, "skills", "intent", "SKILL.md"));
        canonical.Should().Contain("replace-text --old-file");
        canonical.Should().Contain("create --text-file");
        canonical.Should().Contain("link \"$child_id\"");

        var pointer = await File.ReadAllTextAsync(
            Path.Combine(root, ".claude", "skills", "intent", "SKILL.md"));
        pointer.Should().Contain("skills/intent/SKILL.md");
        pointer.Should().NotContain("replace-text --old-file");
    }

    [Theory(DisplayName = "IsTuiReady распознаёт композёр Claude по `❯` промпту и игнорирует пустой/только-сплеш экран")]
    [InlineData("", false)]
    [InlineData("╭─── Claude Code v2.1.116 ───╮\n│  Welcome back Alexander!  │\n╰────────────────────────────╯", false)]
    [InlineData("─────────────\n❯ Try \"how does <filepath> work?\"\n─────────────", true)]
    public void Is_tui_ready_matches_composer_input_row(string snapshot, bool expected)
    {
        var sut = NewAdapter();

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

    private static ClaudeSessionHookAdapter NewAdapter() =>
        new(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" }, new SessionSkillMaterializer());

    private static void AssertExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.GetUnixFileMode(path).Should().HaveFlag(UnixFileMode.UserExecute);
    }
}
