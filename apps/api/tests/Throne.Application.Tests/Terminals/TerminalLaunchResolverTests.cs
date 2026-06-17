using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;

namespace Throne.Application.Tests.Terminals;

public class TerminalLaunchResolverTests
{
    private static TerminalLaunchResolver Build(
        string defaultVendor = TerminalAgentCatalog.VendorClaude,
        IEnumerable<IVendorModelCatalog>? dynamicCatalogs = null,
        bool opencodeAvailable = true)
    {
        var store = Substitute.For<ITerminalSettingsStore>();
        store.GetDefaultVendorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(defaultVendor));
        var capabilities = Substitute.For<ICapabilityAvailability>();
        capabilities.IsAvailableAsync(CapabilityNames.Opencode, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(opencodeAvailable));
        return new TerminalLaunchResolver(
            store,
            dynamicCatalogs ?? Array.Empty<IVendorModelCatalog>(),
            capabilities);
    }

    [Fact(DisplayName = "Пропущенный vendor берётся из настроек, model/effort — нативные дефолты вендора")]
    public async Task Missing_fields_fall_back_to_settings_and_native_defaults()
    {
        var resolver = Build(defaultVendor: TerminalAgentCatalog.VendorCodex);

        var options = await resolver.ResolveAsync(vendor: null, model: null, effort: null, CancellationToken.None);

        options.Vendor.Should().Be(TerminalAgentCatalog.VendorCodex);
        options.Model.Should().Be("gpt-5.5");
        options.Effort.Should().Be(TerminalAgentCatalog.EffortMedium);
    }

    [Fact(DisplayName = "Явный claude получает нативный дефолт effort=high")]
    public async Task Explicit_claude_defaults_to_high_effort()
    {
        var resolver = Build();

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorClaude, model: null, effort: null, CancellationToken.None);

        options.Model.Should().Be("opus");
        options.Effort.Should().Be(TerminalAgentCatalog.EffortHigh);
    }

    [Fact(DisplayName = "Модель вне курируемого whitelist вендора → terminal.args_invalid")]
    public async Task Model_outside_vendor_whitelist_throws()
    {
        var resolver = Build();

        var act = () => resolver.ResolveAsync(
            TerminalAgentCatalog.VendorClaude, model: "gpt-5.5", effort: null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalArgsInvalid);
    }

    [Fact(DisplayName = "Курируемая модель и явный effort проходят без изменений")]
    public async Task Whitelisted_model_and_effort_pass_through()
    {
        var resolver = Build();

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorCodex, model: "gpt-5.3-codex", effort: TerminalAgentCatalog.EffortXhigh,
            CancellationToken.None);

        options.Should().Be(new TerminalLaunchOptions(
            TerminalAgentCatalog.VendorCodex, "gpt-5.3-codex", TerminalAgentCatalog.EffortXhigh));
    }

    [Fact(DisplayName = "Opencode: модель из live /v1/models, effort null")]
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

    [Fact(DisplayName = "Opencode: модель вне live /v1/models → terminal.args_invalid")]
    public async Task Opencode_rejects_model_outside_dynamic_catalog()
    {
        var catalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);
        var resolver = Build(dynamicCatalogs: [catalog]);

        var act = () => resolver.ResolveAsync(
            TerminalAgentCatalog.VendorOpencode, model: "unknown", effort: null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalArgsInvalid);
    }

    [Fact(DisplayName = "Opencode: явный effort молча игнорируется, когда descriptor его не поддерживает")]
    public async Task Opencode_drops_caller_effort()
    {
        var catalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);
        var resolver = Build(dynamicCatalogs: [catalog]);

        var options = await resolver.ResolveAsync(
            TerminalAgentCatalog.VendorOpencode, model: "llama-4",
            effort: TerminalAgentCatalog.EffortHigh, CancellationToken.None);

        options.Effort.Should().BeNull();
    }

    [Fact(DisplayName = "Opencode: capability отключена/не задетекчена → capability.disabled")]
    public async Task Opencode_rejected_when_capability_unavailable()
    {
        var catalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);
        var resolver = Build(dynamicCatalogs: [catalog], opencodeAvailable: false);

        var act = () => resolver.ResolveAsync(
            TerminalAgentCatalog.VendorOpencode, model: "llama-4", effort: null, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityDisabled);
        ex.Which.Extensions.Should().Contain("capability", CapabilityNames.Opencode);
    }

    private sealed class StubCatalog(string vendor, IReadOnlyList<string> models) : IVendorModelCatalog
    {
        public string Vendor { get; } = vendor;
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) => Task.FromResult(models);
    }
}
