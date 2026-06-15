using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalVendorDescriptorTests
{
    [Fact(DisplayName = "claude descriptor: эффорт поддержан, нативный дефолт high, модель — opus")]
    public void Claude_descriptor_supports_effort_high()
    {
        var descriptor = TerminalAgentCatalog.DescriptorFor(TerminalAgentCatalog.VendorClaude);

        descriptor.SupportsEffort.Should().BeTrue();
        descriptor.DefaultEffort.Should().Be(TerminalAgentCatalog.EffortHigh);
        descriptor.DefaultModel.Should().Be("opus");
    }

    [Fact(DisplayName = "codex descriptor: эффорт поддержан, нативный дефолт medium")]
    public void Codex_descriptor_supports_effort_medium()
    {
        var descriptor = TerminalAgentCatalog.DescriptorFor(TerminalAgentCatalog.VendorCodex);

        descriptor.SupportsEffort.Should().BeTrue();
        descriptor.DefaultEffort.Should().Be(TerminalAgentCatalog.EffortMedium);
    }

    [Fact(DisplayName = "no-effort descriptor: DefaultEffort=null и base-args без флага усилия")]
    public void No_effort_descriptor_emits_no_effort_flag()
    {
        // Узкий синтетический descriptor: вендор без оси reasoning effort. Реальный
        // opencode-вендор в этот слайс не вводится — проверяем сам контракт descriptor'а.
        var descriptor = new TerminalVendorDescriptor(
            Vendor: "synthetic",
            Label: "Synthetic",
            Models: ["local-model"],
            SupportsEffort: false,
            Efforts: [],
            DefaultEffort: null,
            ModelSource: TerminalAgentCatalog.ModelSourceStatic,
            BuildBaseArgs: static options => ["--model", options.Model]);

        descriptor.DefaultEffort.Should().BeNull();

        var options = new TerminalLaunchOptions(descriptor.Vendor, descriptor.DefaultModel, Effort: null);
        var args = descriptor.BuildBaseArgs(options);

        args.Should().Equal("--model", "local-model");
        args.Should().NotContain(a => a.Contains("effort", StringComparison.Ordinal));
    }
}
