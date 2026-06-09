using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalLaunchResolverTests
{
    private static TerminalLaunchResolver Build(string defaultVendor = TerminalAgentCatalog.VendorClaude)
    {
        var store = Substitute.For<ITerminalSettingsStore>();
        store.GetDefaultVendorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(defaultVendor));
        return new TerminalLaunchResolver(store);
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
}
