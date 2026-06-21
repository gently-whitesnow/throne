using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Throne.Application.LocalModels;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeSessionSkillPackageTests
{
    private static readonly SessionHookOptions HookOptions = new() { ApiBaseUrl = "http://localhost:5008/" };

    [Fact(DisplayName = "OpenCode review: стейджит artifact writer и hint в instructions")]
    public async Task Review_writes_artifact_script_and_instruction_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            skillPackages: [new ReviewSessionSkillPackage(ReviewTarget())],
            CancellationToken.None);

        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-review"));
        script.Should().NotContain("binding-1");
        script.Should().Contain("THRONE_REPOSITORY_BINDING_ID");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        var instructions = doc.RootElement.GetProperty("instructions");
        instructions.EnumerateArray().Select(i => i.GetString())
            .Should().Contain("throne-session.review.md");

        var hint = await File.ReadAllTextAsync(Path.Combine(root, "throne-session.review.md"));
        hint.Should().Contain("review_recommendation");
        hint.Should().Contain("bin/throne-review write");
    }

    [Fact(DisplayName = "OpenCode interview: пишет intent script/hint без mcp config")]
    public async Task Interview_writes_intent_operations_script_and_instruction_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1",
            root,
            TerminalRunModes.Interview,
            systemPrompt: null,
            skillPackages: [new IntentSessionSkillPackage()],
            CancellationToken.None);

        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-intent"));
        script.Should().NotContain("intent-1");
        script.Should().Contain("THRONE_INTENT_ID");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        doc.RootElement.GetProperty("instructions").EnumerateArray().Select(i => i.GetString())
            .Should().Contain("throne-session.intent.md");
        doc.RootElement.TryGetProperty("mcp", out _).Should().BeFalse();

        var hint = await File.ReadAllTextAsync(Path.Combine(root, "throne-session.intent.md"));
        hint.Should().Contain("Throne Intent Operations");
        hint.Should().Contain("create --text-file");
    }

    private static OpencodeSessionHookAdapter NewAdapter(
        string? baseUrl,
        IReadOnlyList<string> models)
    {
        var catalog = Substitute.For<ILocalModelCatalogPort>();
        catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(models));
        var discovery = new LocalModelDiscoveryService(new LocalModelSettings { BaseUrl = baseUrl }, catalog);

        return new OpencodeSessionHookAdapter(
            discovery,
            HookOptions,
            new FixedServeGateway(),
            new NoopTuiClient());
    }

    private sealed class FixedServeGateway : IOpencodeServeGateway
    {
        public Task<Uri> EnsureRunningAsync(CancellationToken ct) =>
            Task.FromResult(new Uri("http://127.0.0.1:4096/"));
    }

    private sealed class NoopTuiClient : IOpencodeTuiClient
    {
        public Task<string> CreateSessionAndSubmitAsync(
            Uri endpoint,
            string workspacePath,
            string providerId,
            string modelId,
            string prompt,
            CancellationToken ct) =>
            Task.FromResult("unused");
    }

    private static ReviewArtifactWriteTarget ReviewTarget() =>
        new("binding-1", new RepoCoordinate(GitProviderNames.GitHub, "octo", "repo"));
}
