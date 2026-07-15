using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalLaunchResolverTests
{
    private static TerminalLaunchResolver Build(
        string defaultVendor = TerminalAgentCatalog.VendorClaude,
        IEnumerable<IVendorModelCatalog>? dynamicCatalogs = null)
    {
        var store = Substitute.For<ITerminalSettingsStore>();
        store.GetDefaultVendorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(defaultVendor));
        return new TerminalLaunchResolver(
            store,
            TerminalSpawnTestDoubles.VendorCatalog(),
            dynamicCatalogs ?? Array.Empty<IVendorModelCatalog>());
    }

    [Fact(DisplayName = "Missing vendor falls back to settings; native model/effort defaults")]
    public async Task Missing_fields_fall_back_to_settings_and_native_defaults()
    {
        var resolver = Build(defaultVendor: TerminalAgentCatalog.VendorCodex);

        var options = await resolver.ResolveAsync(vendor: null, model: null, effort: null, CancellationToken.None);

        options.Vendor.Should().Be(TerminalAgentCatalog.VendorCodex);
        options.Model.Should().Be("terra");
        options.Effort.Should().Be(TerminalAgentCatalog.EffortHigh);
    }

    [Fact(DisplayName = "Explicit claude defaults to high effort")]
    public async Task Explicit_claude_defaults_to_high_effort()
    {
        var resolver = Build();

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorClaude, model: null, effort: null, CancellationToken.None);

        options.Model.Should().Be("opus");
        options.Effort.Should().Be(TerminalAgentCatalog.EffortHigh);
    }

    [Fact(DisplayName = "Model outside curated whitelist → terminal.args_invalid")]
    public async Task Model_outside_vendor_whitelist_throws()
    {
        var resolver = Build();

        var act = () => resolver.ResolveAsync(
            TerminalAgentCatalog.VendorClaude, model: "terra", effort: null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(TerminalErrorCodes.ArgsInvalid);
    }

    [Fact(DisplayName = "Curated model and explicit effort pass through")]
    public async Task Whitelisted_model_and_effort_pass_through()
    {
        var resolver = Build();

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorCodex, model: "sol", effort: TerminalAgentCatalog.EffortXhigh,
            CancellationToken.None);

        options.Should().Be(new TerminalLaunchOptions(
            TerminalAgentCatalog.VendorCodex, "sol", TerminalAgentCatalog.EffortXhigh));
    }

    [Fact(DisplayName = "Opencode: model resolved from live /v1/models, effort null")]
    public async Task Opencode_resolves_model_from_dynamic_catalog_without_effort()
    {
        var catalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4", "qwen-3"]);
        var resolver = Build(dynamicCatalogs: [catalog]);

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorOpencode, model: "qwen-3", effort: null, CancellationToken.None);

        options.Vendor.Should().Be(TerminalAgentCatalog.VendorOpencode);
        options.Model.Should().Be("qwen-3");
        options.Effort.Should().BeNull();
    }

    [Fact(DisplayName = "Opencode: unknown model → terminal.args_invalid")]
    public async Task Opencode_rejects_model_outside_dynamic_catalog()
    {
        var catalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);
        var resolver = Build(dynamicCatalogs: [catalog]);

        var act = () => resolver.ResolveAsync(
            TerminalAgentCatalog.VendorOpencode, model: "unknown", effort: null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(TerminalErrorCodes.ArgsInvalid);
    }

    [Fact(DisplayName = "Opencode: caller-supplied effort silently dropped (descriptor unsupported)")]
    public async Task Opencode_drops_caller_effort()
    {
        var catalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);
        var resolver = Build(dynamicCatalogs: [catalog]);

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorOpencode, model: "llama-4",
            effort: TerminalAgentCatalog.EffortHigh, CancellationToken.None);

        options.Effort.Should().BeNull();
    }

    private sealed class StubCatalog(string vendor, IReadOnlyList<string> models) : IVendorModelCatalog
    {
        public string Vendor { get; } = vendor;
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) => Task.FromResult(models);
    }
}
