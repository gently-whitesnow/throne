using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Domain.Repositories;
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

    [Fact(DisplayName = "Codex review: стейджит artifact writer и hint в developer profile")]
    public async Task Review_writes_artifact_script_and_profile_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        var home = NewHome();
        var sut = NewAdapter("http://localhost:5008", home);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            skillPackages: [new ReviewSessionSkillPackage(ReviewTarget())],
            CancellationToken.None);

        args.Should().ContainInOrder("-p", "throne-intent-1");
        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-review"));
        script.Should().NotContain("binding-1");
        script.Should().Contain("THRONE_REPOSITORY_BINDING_ID");

        var profile = await File.ReadAllTextAsync(Path.Combine(home, "throne-intent-1.config.toml"));
        profile.Should().Contain("review_recommendation");
        profile.Should().Contain("bin/throne-review write");
    }

    [Fact(DisplayName = "Codex interview: пишет статический intent script и hint-profile")]
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
            skillPackages: [new IntentSessionSkillPackage()],
            CancellationToken.None);

        args.Should().ContainInOrder("-p", "throne-intent-1");
        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-intent"));
        script.Should().NotContain("intent-1");
        script.Should().Contain("THRONE_INTENT_ID");

        var profile = await File.ReadAllTextAsync(Path.Combine(home, "throne-intent-1.config.toml"));
        profile.Should().Contain("Throne Intent Operations");
        profile.Should().Contain("replace-text --old-file");
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

    private static ReviewArtifactWriteTarget ReviewTarget() =>
        new("binding-1", new RepoCoordinate(GitProviderNames.GitHub, "octo", "repo"));
}
