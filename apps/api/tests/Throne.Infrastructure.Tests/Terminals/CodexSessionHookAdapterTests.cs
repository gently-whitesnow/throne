using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class CodexSessionHookAdapterTests
{
    [Fact(DisplayName = "Codex: инлайн -c hooks.Stop + hooks.UserPromptSubmit с curl на локальный API + bypass-hook-trust")]
    public async Task Builds_inline_hook_overrides()
    {
        var sut = new CodexSessionHookAdapter(new SessionHookOptions
        {
            ApiBaseUrl = "http://localhost:5008/",
        });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", workspacePath: "/unused", TerminalRunModes.Work, CancellationToken.None);

        args.Should().Equal(
            "-c",
            "hooks.Stop=[{hooks=[{type=\"command\",command=\"" +
            "curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/Stop?mode=work' " +
            "-H 'Content-Type: application/json' -d @-\",timeout=10}]}]",
            "-c",
            "hooks.UserPromptSubmit=[{hooks=[{type=\"command\",command=\"" +
            "curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/UserPromptSubmit?mode=work' " +
            "-H 'Content-Type: application/json' -d @-\",timeout=10}]}]",
            "--dangerously-bypass-hook-trust");
    }

    [Fact(DisplayName = "Codex: spawn-режим попадает в hook-URL как query mode — interview")]
    public async Task Bakes_spawn_mode_into_hook_url()
    {
        var sut = new CodexSessionHookAdapter(new SessionHookOptions { ApiBaseUrl = "http://localhost:5008" });

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", workspacePath: "/unused", TerminalRunModes.Interview, CancellationToken.None);

        args.Should().Contain(t => t.Contains("/hooks/UserPromptSubmit?mode=interview'"));
        args.Should().Contain(t => t.Contains("/hooks/Stop?mode=interview'"));
    }

    [Fact(DisplayName = "Codex: per-session слой инлайновый — никаких файлов в workspace")]
    public async Task Writes_no_file_into_workspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sut = new CodexSessionHookAdapter(new SessionHookOptions());

        await sut.PrepareSpawnArgsAsync("intent-1", root, TerminalRunModes.Work, CancellationToken.None);

        Directory.GetFileSystemEntries(root).Should().BeEmpty();
    }
}
