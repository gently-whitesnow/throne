using System.Text.Json;
using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class ClaudeSessionHookAdapterTests
{
    [Fact(DisplayName = "Пишет per-session settings со Stop- и UserPromptSubmit-hook'ами и возвращает --settings <файл>")]
    public async Task Writes_both_hooks_and_returns_flag()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions
        {
            ApiBaseUrl = "http://localhost:5008/",
        });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, CancellationToken.None);

        var settingsPath = Path.Combine(root, "throne-session.settings.json");
        args.Should().Equal("--settings", settingsPath);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        HookCommand(document, "Stop").Should().Be(
            "curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/Stop?mode=work' " +
            "-H 'Content-Type: application/json' -d @-");
        HookCommand(document, "UserPromptSubmit").Should().Be(
            "curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/UserPromptSubmit?mode=work' " +
            "-H 'Content-Type: application/json' -d @-");
    }

    [Fact(DisplayName = "Непустой systemPrompt пишется в файл и подаётся через --append-system-prompt-file")]
    public async Task Writes_system_prompt_file_and_references_it()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "RULES\nblock", CancellationToken.None);

        var settingsPath = Path.Combine(root, "throne-session.settings.json");
        var systemPromptPath = Path.Combine(root, "throne-session.append-system-prompt.txt");
        args.Should().Equal("--settings", settingsPath, "--append-system-prompt-file", systemPromptPath);
        (await File.ReadAllTextAsync(systemPromptPath)).Should().Be("RULES\nblock");
    }

    [Fact(DisplayName = "Пустой systemPrompt не пишет файл и не добавляет флаг")]
    public async Task Blank_system_prompt_writes_no_file()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "   ", CancellationToken.None);

        args.Should().NotContain("--append-system-prompt-file");
        File.Exists(Path.Combine(root, "throne-session.append-system-prompt.txt")).Should().BeFalse();
    }

    [Fact(DisplayName = "Spawn-режим попадает в hook-URL как query mode — interview")]
    public async Task Bakes_spawn_mode_into_hook_url()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Interview, systemPrompt: null, CancellationToken.None);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "throne-session.settings.json")));
        HookCommand(document, "UserPromptSubmit").Should().Contain("/hooks/UserPromptSubmit?mode=interview'");
    }

    private static string? HookCommand(JsonDocument document, string hookEvent) =>
        document.RootElement
            .GetProperty("hooks")
            .GetProperty(hookEvent)[0]
            .GetProperty("hooks")[0]
            .GetProperty("command")
            .GetString();
}
