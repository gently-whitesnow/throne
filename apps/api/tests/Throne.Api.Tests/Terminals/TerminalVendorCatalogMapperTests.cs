using FluentAssertions;
using Throne.Api.Terminals;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Tests.Terminals;

public class TerminalVendorCatalogMapperTests
{
    [Fact(DisplayName = "Каталог: default_vendor=claude и оба вендора отданы в порядке каталога")]
    public void Maps_default_vendor_and_vendor_order()
    {
        var dto = TerminalVendorCatalogMapper.ToDto();

        dto.Default_vendor.Should().Be(TerminalAgentVendor.Claude);
        dto.Vendors.Select(v => v.Vendor)
            .Should().Equal(TerminalAgentVendor.Claude, TerminalAgentVendor.Codex);
    }

    [Fact(DisplayName = "claude metadata: модели opus-first, дефолт opus, эффорт high, источник static")]
    public void Maps_claude_metadata()
    {
        var claude = TerminalVendorCatalogMapper.ToDto().Vendors
            .Single(v => v.Vendor == TerminalAgentVendor.Claude);

        claude.Label.Should().Be("Claude");
        claude.Models.Should().Equal("opus", "sonnet", "haiku");
        claude.Default_model.Should().Be("opus");
        claude.Supports_effort.Should().BeTrue();
        claude.Default_effort.Should().Be(TerminalReasoningEffort.High);
        claude.Efforts.Should().Equal(
            TerminalReasoningEffort.Low,
            TerminalReasoningEffort.Medium,
            TerminalReasoningEffort.High,
            TerminalReasoningEffort.Xhigh);
        claude.Model_source.Should().Be(TerminalModelSource.Static);
    }

    [Fact(DisplayName = "codex metadata: дефолт-модель первая из списка, эффорт medium")]
    public void Maps_codex_metadata()
    {
        var codex = TerminalVendorCatalogMapper.ToDto().Vendors
            .Single(v => v.Vendor == TerminalAgentVendor.Codex);

        codex.Default_model.Should().Be(codex.Models.First());
        codex.Models.Should().Equal("gpt-5.5", "gpt-5.4", "gpt-5.3-codex");
        codex.Supports_effort.Should().BeTrue();
        codex.Default_effort.Should().Be(TerminalReasoningEffort.Medium);
        codex.Model_source.Should().Be(TerminalModelSource.Static);
    }

    [Fact(DisplayName = "Каждый отданный вендор повторяет данные своего descriptor'а в каталоге")]
    public void Vendor_dtos_mirror_descriptors()
    {
        var dto = TerminalVendorCatalogMapper.ToDto();

        dto.Vendors.Should().HaveCount(TerminalAgentCatalog.Descriptors.Count);
        foreach (var vendorDto in dto.Vendors)
        {
            var descriptor = TerminalAgentCatalog.DescriptorFor(
                vendorDto.Vendor == TerminalAgentVendor.Claude
                    ? TerminalAgentCatalog.VendorClaude
                    : TerminalAgentCatalog.VendorCodex);

            vendorDto.Models.Should().Equal(descriptor.Models);
            vendorDto.Default_model.Should().Be(descriptor.DefaultModel);
            vendorDto.Supports_effort.Should().Be(descriptor.SupportsEffort);
        }
    }
}
