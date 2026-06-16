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
    private static LocalModelDiscoveryService BuildDiscovery(
        string? baseUrl, IReadOnlyList<string> models)
    {
        var catalog = Substitute.For<ILocalModelCatalogPort>();
        catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(models));
        return new LocalModelDiscoveryService(new LocalModelSettings { BaseUrl = baseUrl }, catalog);
    }

    [Fact(DisplayName = "Пишет opencode.json с provider throne-local, baseURL и models map; argv пустой")]
    public async Task Writes_opencode_config_and_returns_empty_argv()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = new OpencodeSessionHookAdapter(
            BuildDiscovery("http://localhost:1234", ["llama-4", "qwen-3"]));

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, CancellationToken.None);

        args.Should().BeEmpty();
        var configPath = Path.Combine(root, "opencode.json");
        File.Exists(configPath).Should().BeTrue();

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        doc.RootElement.GetProperty("$schema").GetString().Should().Be("https://opencode.ai/config.json");
        var provider = doc.RootElement.GetProperty("provider").GetProperty("throne-local");
        provider.GetProperty("npm").GetString().Should().Be("@ai-sdk/openai-compatible");
        provider.GetProperty("options").GetProperty("baseURL").GetString().Should().Be("http://localhost:1234");
        var models = provider.GetProperty("models");
        models.GetProperty("llama-4").GetProperty("name").GetString().Should().Be("llama-4");
        models.GetProperty("qwen-3").GetProperty("name").GetString().Should().Be("qwen-3");
        doc.RootElement.TryGetProperty("instructions", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Непустой systemPrompt пишется в файл и попадает в instructions по имени")]
    public async Task Writes_system_prompt_and_references_it_by_filename()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = new OpencodeSessionHookAdapter(
            BuildDiscovery("http://localhost:1234", ["llama-4"]));

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "RULES\nblock", CancellationToken.None);

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
        var sut = new OpencodeSessionHookAdapter(
            BuildDiscovery("http://localhost:1234", ["llama-4"]));

        await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: "   ", CancellationToken.None);

        File.Exists(Path.Combine(root, "throne-session.append-system-prompt.txt")).Should().BeFalse();
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        doc.RootElement.TryGetProperty("instructions", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Пустой live discovery — opencode.json валиден с пустой картой моделей")]
    public async Task Empty_discovery_writes_empty_models_map()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-opencode-{Guid.NewGuid():N}");
        var sut = new OpencodeSessionHookAdapter(BuildDiscovery(baseUrl: null, models: []));

        var args = await sut.PrepareSpawnArgsAsync(
            "intent-1", root, TerminalRunModes.Work, systemPrompt: null, CancellationToken.None);

        args.Should().BeEmpty();
        using var doc = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "opencode.json")));
        var provider = doc.RootElement.GetProperty("provider").GetProperty("throne-local");
        provider.GetProperty("options").GetProperty("baseURL").GetString().Should().BeEmpty();
        provider.GetProperty("models").EnumerateObject().Should().BeEmpty();
    }

    [Theory(DisplayName = "IsTuiReady видит композёр OpenCode по '>' в начале строки")]
    [InlineData("", false)]
    [InlineData("opencode v0.5.0 — starting up", false)]
    [InlineData("intent task summary > preview\nrunning", false)]
    [InlineData("> ", true)]
    [InlineData("───\n> Tell OpenCode what to do…\n───", true)]
    public void Is_tui_ready_matches_composer_glyph_at_line_start(string snapshot, bool expected)
    {
        var sut = new OpencodeSessionHookAdapter(BuildDiscovery("http://localhost", []));

        sut.IsTuiReady(snapshot).Should().Be(expected);
    }
}
