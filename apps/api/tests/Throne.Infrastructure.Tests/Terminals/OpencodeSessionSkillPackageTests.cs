using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Throne.Application.LocalModels;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeSessionSkillPackageTests
{
    private static readonly SessionHookOptions HookOptions = new() { ApiBaseUrl = "http://localhost:5008/" };

    [Fact(DisplayName = "OpenCode review: запекает artifact writer и hint в instructions")]
    public async Task Review_writes_artifact_script_and_instruction_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            skillPackages: [new ReviewArtifactSessionSkillPackage(new ReviewArtifactWriteTarget("binding-1", 42))],
            CancellationToken.None);

        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-pr-artifact-write"));
        script.Should().Contain("BINDING_ID='binding-1'");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        var instructions = doc.RootElement.GetProperty("instructions");
        instructions.EnumerateArray().Select(i => i.GetString())
            .Should().Contain("throne-session.review-artifact.md");

        var hint = await File.ReadAllTextAsync(Path.Combine(root, "throne-session.review-artifact.md"));
        hint.Should().Contain("review_recommendation");
        hint.Should().Contain("send-comments");
    }

    [Fact(DisplayName = "OpenCode interview: пишет intent-ops script/hint без Throne MCP")]
    public async Task Interview_writes_intent_operations_script_and_instruction_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1",
            root,
            TerminalRunModes.Interview,
            systemPrompt: null,
            skillPackages: [new IntentOperationsSessionSkillPackage("intent-1")],
            CancellationToken.None);

        var script = await File.ReadAllTextAsync(Path.Combine(root, "bin", "throne-intent"));
        script.Should().Contain("INTENT_ID='intent-1'");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        doc.RootElement.GetProperty("instructions").EnumerateArray().Select(i => i.GetString())
            .Should().Contain("throne-session.intent-ops.md");
        doc.RootElement.TryGetProperty("mcp", out _).Should().BeFalse();

        var hint = await File.ReadAllTextAsync(Path.Combine(root, "throne-session.intent-ops.md"));
        hint.Should().Contain("Throne intent operations");
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
}
