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

    [Fact(DisplayName = "opencode descriptor: пустой spawn argv (loop в shared serve), эффорта нет, ModelSource=local")]
    public void Opencode_descriptor_emits_no_base_args_and_has_no_effort()
    {
        var descriptor = TerminalAgentCatalog.DescriptorFor(TerminalAgentCatalog.VendorOpencode);

        descriptor.SupportsEffort.Should().BeFalse();
        descriptor.DefaultEffort.Should().BeNull();
        descriptor.ModelSource.Should().Be(TerminalAgentCatalog.ModelSourceLocal);
        descriptor.Models.Should().BeEmpty();
        descriptor.DefaultModel.Should().BeNull();

        var options = new TerminalLaunchOptions(
            TerminalAgentCatalog.VendorOpencode, Model: "llama-4", Effort: null);

        // The pane runs `opencode attach …` (argv supplied by the session-hook adapter), not a
        // model-flagged TUI: the model is pinned server-side on the prompt, so no base flags here.
        descriptor.BuildBaseArgs(options).Should().BeEmpty();
    }
}
