using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Throne.Application.LocalModels;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeSessionHookAdapterTests
{
    private static readonly SessionHookOptions HookOptions = new() { ApiBaseUrl = "http://localhost:5008/" };

    private static LocalModelDiscoveryService BuildDiscovery(
        string? baseUrl, IReadOnlyList<string> models)
    {
        var catalog = Substitute.For<ILocalModelCatalogPort>();
        catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(models));
        return new LocalModelDiscoveryService(new LocalModelSettings { BaseUrl = baseUrl }, catalog);
    }

    [Fact(DisplayName = "Пишет opencode.json и возвращает пустой spawn argv (loop живёт в shared serve)")]
    public async Task Writes_opencode_config_and_returns_empty_argv()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4", "qwen-3"]);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, reviewArtifact: null, CancellationToken.None);

        args.Should().BeEmpty();
        var configPath = Path.Combine(root, "opencode.json");
        File.Exists(configPath).Should().BeTrue();
        File.Exists(Path.Combine(root, ".opencode", "plugins", "throne.js")).Should().BeTrue();

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        doc.RootElement.GetProperty("$schema").GetString().Should().Be("https://opencode.ai/config.json");
        var provider = doc.RootElement.GetProperty("provider").GetProperty("throne-local");
        provider.GetProperty("npm").GetString().Should().Be("@ai-sdk/openai-compatible");
        provider.GetProperty("options").GetProperty("baseURL").GetString().Should().Be("http://localhost:1234");
        var models = provider.GetProperty("models");
        models.GetProperty("llama-4").GetProperty("name").GetString().Should().Be("llama-4");
        models.GetProperty("qwen-3").GetProperty("name").GetString().Should().Be("qwen-3");
        var mcp = doc.RootElement.GetProperty("mcp").GetProperty("throne");
        mcp.GetProperty("type").GetString().Should().Be("remote");
        mcp.GetProperty("url").GetString().Should().Be("http://localhost:5008/mcp");
        mcp.GetProperty("enabled").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("instructions", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "OpenCode MCP: сохраняет чужие mcp-серверы при перезаписи opencode.json")]
    public async Task Preserves_existing_mcp_servers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "opencode.json"), """
        {
          "mcp": {
            "github": { "type": "remote", "url": "https://example.test/mcp", "enabled": false },
            "throne": { "type": "remote", "url": "http://old/mcp", "enabled": true }
          }
        }
        """);
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, reviewArtifact: null, CancellationToken.None);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        var mcp = doc.RootElement.GetProperty("mcp");
        mcp.GetProperty("github").GetProperty("url").GetString().Should().Be("https://example.test/mcp");
        mcp.GetProperty("github").GetProperty("enabled").GetBoolean().Should().BeFalse();
        mcp.GetProperty("throne").GetProperty("url").GetString().Should().Be("http://localhost:5008/mcp");
    }

    [Fact(DisplayName = "Непустой systemPrompt пишется в файл и попадает в instructions по имени")]
    public async Task Writes_system_prompt_and_references_it_by_filename()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "RULES\nblock", reviewArtifact: null, CancellationToken.None);

        var promptPath = Path.Combine(root, "throne-session.append-system-prompt.txt");
        (await File.ReadAllTextAsync(promptPath)).Should().Be("RULES\nblock");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        var instructions = doc.RootElement.GetProperty("instructions");
        instructions.GetArrayLength().Should().Be(1);
        instructions[0].GetString().Should().Be("throne-session.append-system-prompt.txt");
    }

    [Fact(DisplayName = "Пустой systemPrompt не пишет файл и не добавляет instructions")]
    public async Task Blank_system_prompt_writes_no_file()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "   ", reviewArtifact: null, CancellationToken.None);

        File.Exists(Path.Combine(root, "throne-session.append-system-prompt.txt")).Should().BeFalse();
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        doc.RootElement.TryGetProperty("instructions", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Пустой live discovery — opencode.json валиден с пустой картой моделей")]
    public async Task Empty_discovery_writes_empty_models_map()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter(baseUrl: null, models: []);

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, reviewArtifact: null, CancellationToken.None);

        args.Should().BeEmpty();
        using var doc = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        var provider = doc.RootElement.GetProperty("provider").GetProperty("throne-local");
        provider.GetProperty("options").GetProperty("baseURL").GetString().Should().BeEmpty();
        provider.GetProperty("models").EnumerateObject().Should().BeEmpty();
    }

    [Fact(DisplayName = "OpenCode readiness объявляется через SessionReady, glyph-scrape отключён")]
    public void Opencode_readiness_uses_hook_event_not_glyph_scrape()
    {
        var sut = NewAdapter("http://localhost", []);

        sut.ReadinessHookEvent.Should().Be(TerminalHookEvents.SessionReady);
        sut.IsTuiReady("───\n> Tell OpenCode what to do…\n───").Should().BeFalse();
    }

    [Fact(DisplayName = "Непустой prompt: создаёт сессию на shared serve и строит attach --session argv")]
    public async Task Non_empty_prompt_creates_session_and_builds_attach_args()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var client = new RecordingTuiClient();
        var sut = NewAdapter("http://localhost:1234", ["llama-4"], client);

        var args = await sut.InitializeSessionAsync("intent-1", root, "qwen-3", "TASK", CancellationToken.None);

        args.Should().Equal("attach", "http://127.0.0.1:4096", "--dir", root, "--session", "ses_made");
        client.Calls.Should().Equal(
            new TuiCall(new Uri("http://127.0.0.1:4096/"), root, "throne-local", "qwen-3", "TASK"));
    }

    [Fact(DisplayName = "Пустой prompt: attach без --session, сессия не создаётся (boot bare)")]
    public async Task Blank_prompt_attaches_without_session()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var client = new RecordingTuiClient();
        var sut = NewAdapter("http://localhost:1234", ["llama-4"], client);

        var args = await sut.InitializeSessionAsync("intent-1", root, "qwen-3", "   ", CancellationToken.None);

        args.Should().Equal("attach", "http://127.0.0.1:4096", "--dir", root);
        client.Calls.Should().BeEmpty();
    }

    [Fact(DisplayName = "Plugin shim маппит OpenCode lifecycle events в существующий hook endpoint")]
    public async Task Plugin_shim_maps_lifecycle_events_to_hook_endpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Interview, systemPrompt: null, reviewArtifact: null, CancellationToken.None);

        var calls = await RunPluginSmokeAsync(root);
        calls.Should().Equal(
            "curl -s -X POST http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/SessionReady?mode=interview",
            "curl -s -X POST http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/Stop?mode=interview",
            "curl -s -X POST http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/UserPromptSubmit?mode=interview",
            "curl -s -X POST http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/Notification?mode=interview",
            "curl -s -X POST http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/PostToolUse?mode=interview",
            "curl -s -X POST http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/PostToolUse?mode=interview");
    }

    [Fact(DisplayName = "OpenCode review: запекает artifact writer и hint в instructions")]
    public async Task Review_writes_artifact_script_and_instruction_hint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = NewAdapter("http://localhost:1234", ["llama-4"]);

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Review, systemPrompt: null,
            reviewArtifact: new ReviewArtifactWriteTarget("binding-1", 42), CancellationToken.None);

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

    [Fact(DisplayName = "OpenCodeBindings фиксируют подтвержденный минимум CLI и не переиспользуют Claude/Codex bindings")]
    public void Opencode_bindings_are_separate_and_version_pinned()
    {
        OpencodePluginShim.MinimumSupportedCliVersion.Should().Be("1.17.7");
        TerminalHookEvents.OpenCodeBindings
            .Select(binding => (binding.OpenCodeHook, binding.ThroneEvent, binding.BindingType))
            .Should().Equal(
                ("session.created", TerminalHookEvents.SessionReady, TerminalHookEvents.OpenCodeBindingEvent),
                ("session.idle", TerminalHookEvents.Stop, TerminalHookEvents.OpenCodeBindingEvent),
                ("tui.prompt.append", TerminalHookEvents.UserPromptSubmit, TerminalHookEvents.OpenCodeBindingEvent),
                ("permission.asked", TerminalHookEvents.Notification, TerminalHookEvents.OpenCodeBindingEvent),
                ("permission.replied", TerminalHookEvents.PostToolUse, TerminalHookEvents.OpenCodeBindingEvent),
                ("tool.execute.after", TerminalHookEvents.PostToolUse, TerminalHookEvents.OpenCodeBindingTypedHook));
    }

    private static OpencodeSessionHookAdapter NewAdapter(
        string? baseUrl,
        IReadOnlyList<string> models,
        IOpencodeTuiClient? client = null) =>
        new(
            BuildDiscovery(baseUrl, models),
            HookOptions,
            new FixedServeGateway(),
            client ?? new RecordingTuiClient());

    private sealed class FixedServeGateway : IOpencodeServeGateway
    {
        public Task<Uri> EnsureRunningAsync(CancellationToken ct) =>
            Task.FromResult(new Uri("http://127.0.0.1:4096/"));
    }

    private sealed record TuiCall(Uri Endpoint, string WorkspacePath, string ProviderId, string ModelId, string Prompt);

    private sealed class RecordingTuiClient : IOpencodeTuiClient
    {
        public List<TuiCall> Calls { get; } = [];

        public Task<string> CreateSessionAndSubmitAsync(
            Uri endpoint,
            string workspacePath,
            string providerId,
            string modelId,
            string prompt,
            CancellationToken ct)
        {
            Calls.Add(new TuiCall(endpoint, workspacePath, providerId, modelId, prompt));
            return Task.FromResult("ses_made");
        }
    }

    private static async Task<IReadOnlyList<string>> RunPluginSmokeAsync(string root)
    {
        var node = FindExecutable("node");
        node.Should().NotBeNull("OpenCode plugin smoke validates the generated ESM shim");

        await File.WriteAllTextAsync(Path.Combine(root, ".opencode", "package.json"), """{"type":"module"}""");
        var script = Path.Combine(root, ".opencode", "plugins", "smoke.mjs");
        await File.WriteAllTextAsync(script, """
            import { ThroneLifecyclePlugin } from "./throne.js";

            const calls = [];
            const $ = async (strings, ...values) => {
              calls.push(strings.reduce((acc, part, index) => acc + part + (values[index] ?? ""), ""));
            };
            const hooks = await ThroneLifecyclePlugin({ $ });
            await hooks.event({ event: { type: "session.created" } });
            await hooks.event({ event: { type: "session.idle" } });
            await hooks.event({ event: { type: "tui.prompt.append" } });
            await hooks.event({ event: { type: "permission.asked" } });
            await hooks.event({ event: { type: "permission.replied" } });
            await hooks["tool.execute.after"]({}, {});
            console.log(JSON.stringify(calls));
            """);

        var start = new ProcessStartInfo(node!, script)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.Combine(root, ".opencode", "plugins"),
        };
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        process.ExitCode.Should().Be(0, error);
        return JsonSerializer.Deserialize<string[]>(output.Trim())!;
    }

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
