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
            "intent-1", NewWorkspace(), TerminalRunModes.Work, systemPrompt: null, skillPackages: [], CancellationToken.None);

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
            "intent-1", NewWorkspace(), TerminalRunModes.Work, systemPrompt: "say \"hi\"\nline2", skillPackages: [], CancellationToken.None);

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
            "intent-1", NewWorkspace(), TerminalRunModes.Work, systemPrompt: null, skillPackages: [], CancellationToken.None);

        args.Should().NotContain("-p");
        File.Exists(Path.Combine(home, "throne-intent-1.config.toml")).Should().BeFalse();
    }

    [Fact(DisplayName = "Codex: CleanupAsync удаляет профиль интента")]
    public async Task Cleanup_removes_profile()
    {
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);
        await sut.PrepareSpawnArgsAsync(
            "intent-1", NewWorkspace(), TerminalRunModes.Work, systemPrompt: "RULES", skillPackages: [], CancellationToken.None);
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
            "intent-1", NewWorkspace(), TerminalRunModes.Interview, systemPrompt: null, skillPackages: [], CancellationToken.None);

        args.Should().Contain(t => t.Contains("/hooks/UserPromptSubmit?mode=interview'"));
        args.Should().Contain(t => t.Contains("/hooks/Stop?mode=interview'"));
    }

    [Fact(DisplayName = "Codex: без systemPrompt пишет только workspace MCP config")]
    public async Task Writes_workspace_mcp_config_without_profile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sut = NewAdapter("http://localhost:5008");

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, skillPackages: [], CancellationToken.None);

        var configPath = Path.Combine(root, ".codex", "config.toml");
        (await File.ReadAllTextAsync(configPath)).Should().Contain("url = \"http://localhost:5008/mcp\"");
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Should().Equal(configPath);
    }

    [Fact(DisplayName = "Codex review: запекает artifact writer и hint в developer profile")]
    public async Task Review_writes_artifact_script_and_profile_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            skillPackages: [new ReviewArtifactSessionSkillPackage(new ReviewArtifactWriteTarget("binding-1", 42))],
            CancellationToken.None);

        args.Should().ContainInOrder("-p", "throne-intent-1");
        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-pr-artifact-write"));
        script.Should().Contain("BINDING_ID='binding-1'");

        var profile = await File.ReadAllTextAsync(Path.Combine(home, "throne-intent-1.config.toml"));
        profile.Should().Contain("review_recommendation");
        profile.Should().Contain("send-comments");
    }

    [Fact(DisplayName = "Codex interview: пишет intent-ops script и hint-profile без workspace Throne MCP")]
    public async Task Interview_writes_intent_operations_script_and_profile_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1",
            root,
            TerminalRunModes.Interview,
            systemPrompt: null,
            skillPackages: [new IntentOperationsSessionSkillPackage("intent-1")],
            CancellationToken.None);

        args.Should().ContainInOrder("-p", "throne-intent-1");
        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-intent"));
        script.Should().Contain("INTENT_ID='intent-1'");

        var profile = await File.ReadAllTextAsync(Path.Combine(home, "throne-intent-1.config.toml"));
        profile.Should().Contain("Throne intent operations");
        profile.Should().Contain("replace-text --old-file");

        File.Exists(Path.Combine(root, ".codex", "config.toml")).Should().BeFalse();
    }

    [Theory(DisplayName = "Codex IsTuiReady распознаёт композёр по input-row маркеру и игнорирует splash")]
    [InlineData("", false)]
    [InlineData("OpenAI Codex\nloading model…", false)]
    [InlineData("╭─────╮\n│ > _                 │\n╰─────╯", true)]
    public void Is_tui_ready_matches_composer_input_row(string snapshot, bool expected)
    {
        var sut = NewAdapter("http://localhost:5008");

        sut.IsTuiReady(snapshot).Should().Be(expected);
    }

    private static string NewHome()
    {
        var home = Path.Combine(Path.GetTempPath(), $"throne-codexhome-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        return home;
    }

    private static string NewWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static CodexSessionHookAdapter NewAdapter(string apiBaseUrl, string? codexHome = null) =>
        new(new SessionHookOptions { ApiBaseUrl = apiBaseUrl }, codexHome ?? NewHome());
}
