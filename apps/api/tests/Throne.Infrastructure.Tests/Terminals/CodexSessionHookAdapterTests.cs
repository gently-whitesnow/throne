using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class CodexSessionHookAdapterTests
{
    [Fact(DisplayName = "Codex: инлайн -c hooks.Stop + hooks.UserPromptSubmit с curl на локальный API + bypass-hook-trust")]
    public async Task Builds_inline_hook_overrides()
    {
        var sut = NewAdapter("http://localhost:5008/");

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", workspacePath: "/unused", TerminalRunModes.Work, systemPrompt: null, CancellationToken.None);

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

    [Fact(DisplayName = "Codex: непустой systemPrompt пишет профиль $CODEX_HOME/throne-<id>.config.toml и подаёт -p")]
    public async Task Writes_profile_and_references_it()
    {
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", "/unused", TerminalRunModes.Work, systemPrompt: "say \"hi\"\nline2", CancellationToken.None);

        args.Should().ContainInOrder("-p", "throne-intent-1");
        var profilePath = Path.Combine(home, "throne-intent-1.config.toml");
        (await File.ReadAllTextAsync(profilePath)).Should()
            .Be("developer_instructions = \"say \\\"hi\\\"\\nline2\"\n");
    }

    [Fact(DisplayName = "Codex: пустой systemPrompt не пишет профиль и не добавляет -p")]
    public async Task Blank_system_prompt_writes_no_profile()
    {
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", "/unused", TerminalRunModes.Work, systemPrompt: null, CancellationToken.None);

        args.Should().NotContain("-p");
        File.Exists(Path.Combine(home, "throne-intent-1.config.toml")).Should().BeFalse();
    }

    [Fact(DisplayName = "Codex: CleanupAsync удаляет профиль интента")]
    public async Task Cleanup_removes_profile()
    {
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);
        await sut.PrepareSpawnArgsAsync(
            "intent-1", "/unused", TerminalRunModes.Work, systemPrompt: "RULES", CancellationToken.None);
        var profilePath = Path.Combine(home, "throne-intent-1.config.toml");
        File.Exists(profilePath).Should().BeTrue();

        await sut.CleanupAsync("intent-1", CancellationToken.None);

        File.Exists(profilePath).Should().BeFalse();
    }

    [Fact(DisplayName = "Codex: spawn-режим попадает в hook-URL как query mode — interview")]
    public async Task Bakes_spawn_mode_into_hook_url()
    {
        var sut = NewAdapter("http://localhost:5008");

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", workspacePath: "/unused", TerminalRunModes.Interview, systemPrompt: null, CancellationToken.None);

        args.Should().Contain(t => t.Contains("/hooks/UserPromptSubmit?mode=interview'"));
        args.Should().Contain(t => t.Contains("/hooks/Stop?mode=interview'"));
    }

    [Fact(DisplayName = "Codex: без systemPrompt — никаких файлов в workspace")]
    public async Task Writes_no_file_into_workspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sut = NewAdapter("http://localhost:5008");

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, CancellationToken.None);

        Directory.GetFileSystemEntries(root).Should().BeEmpty();
    }

    private static string NewHome()
    {
        var home = Path.Combine(Path.GetTempPath(), $"throne-codexhome-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        return home;
    }

    private static CodexSessionHookAdapter NewAdapter(string apiBaseUrl, string? codexHome = null) =>
        new(new SessionHookOptions { ApiBaseUrl = apiBaseUrl }, codexHome ?? NewHome());
}
