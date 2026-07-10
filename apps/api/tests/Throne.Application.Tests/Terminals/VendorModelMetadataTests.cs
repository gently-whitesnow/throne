using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class VendorModelMetadataTests
{
    [Fact(DisplayName = "Parse: маппит вендоров и сохраняет порядок native-default-first")]
    public void Parses_vendor_lists_and_preserves_order()
    {
        var payload = /* lang=json */ """
            { "vendors": { "claude": ["opus", "sonnet"], "codex": ["gpt-5.5"] } }
            """;

        var map = VendorModelMetadata.Parse(payload);

        map["claude"].Should().Equal("opus", "sonnet");
        map["codex"].Should().Equal("gpt-5.5");
    }

    [Fact(DisplayName = "Parse: пустая секция vendors → падаем сразу")]
    public void Rejects_empty_vendors_section()
    {
        var act = () => VendorModelMetadata.Parse("""{ "vendors": {} }""");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*vendors*empty*");
    }

    [Fact(DisplayName = "Parse: пустой список моделей вендора → падаем")]
    public void Rejects_vendor_with_empty_models()
    {
        var act = () => VendorModelMetadata.Parse("""{ "vendors": { "claude": [] } }""");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*claude*empty*");
    }

    [Fact(DisplayName = "Parse: пустая строка в списке моделей → падаем")]
    public void Rejects_blank_model_id()
    {
        var act = () => VendorModelMetadata.Parse("""{ "vendors": { "claude": [" "] } }""");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*blank*");
    }

    [Fact(DisplayName = "For: embedded ресурс отдаёт три модели claude, лидер — opus")]
    public void For_reads_embedded_resource_with_expected_defaults()
    {
        var claude = VendorModelMetadata.For(TerminalAgentCatalog.VendorClaude);
        claude.Should().Equal("opus", "sonnet", "haiku");

        var codex = VendorModelMetadata.For(TerminalAgentCatalog.VendorCodex);
        codex[0].Should().Be("gpt-5.5");

        VendorModelMetadata.For("no-such-vendor").Should().BeEmpty();
    }
}
