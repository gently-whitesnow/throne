using FluentAssertions;
using NSubstitute;
using Throne.Api.Terminals;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Tests.Terminals;

public class TerminalVendorCatalogMapperTests
{
    private static TerminalVendorCatalogMapper Build(
        IVendorModelCatalog[]? dynamicCatalogs = null,
        bool opencodeAvailable = true)
    {
        var capabilities = Substitute.For<ICapabilityAvailability>();
        capabilities.IsAvailableAsync(CapabilityNames.Opencode, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(opencodeAvailable));
        return new TerminalVendorCatalogMapper(
            dynamicCatalogs ?? Array.Empty<IVendorModelCatalog>(),
            capabilities);
    }

    [Fact(DisplayName = "Каталог: default_vendor=claude и все три вендора отданы в порядке каталога")]
    public async Task Maps_default_vendor_and_vendor_order()
    {
        var dto = await Build().ToDtoAsync(CancellationToken.None);

        dto.Default_vendor.Should().Be(TerminalAgentVendor.Claude);
        dto.Vendors.Select(v => v.Vendor)
            .Should().Equal(TerminalAgentVendor.Claude, TerminalAgentVendor.Codex, TerminalAgentVendor.Opencode);
    }

    [Fact(DisplayName = "claude metadata: модели opus-first, дефолт opus, эффорт high, источник static")]
    public async Task Maps_claude_metadata()
    {
        var dto = await Build().ToDtoAsync(CancellationToken.None);
        var claude = dto.Vendors.Single(v => v.Vendor == TerminalAgentVendor.Claude);

        claude.Label.Should().Be("Claude");
        claude.Models.Should().Equal("opus", "sonnet", "haiku");
        claude.Default_model.Should().Be("opus");
        claude.Supports_effort.Should().BeTrue();
        claude.Default_effort.Should().Be(TerminalReasoningEffort.High);
        claude.Efforts.Should().Equal("low", "medium", "high", "xhigh");
        claude.Model_source.Should().Be(TerminalModelSource.Static);
    }

    [Fact(DisplayName = "codex metadata: дефолт-модель первая из списка, эффорт medium")]
    public async Task Maps_codex_metadata()
    {
        var dto = await Build().ToDtoAsync(CancellationToken.None);
        var codex = dto.Vendors.Single(v => v.Vendor == TerminalAgentVendor.Codex);

        codex.Default_model.Should().Be(codex.Models.First());
        codex.Models.Should().Equal("gpt-5.5", "gpt-5.4", "gpt-5.3-codex");
        codex.Supports_effort.Should().BeTrue();
        codex.Default_effort.Should().Be(TerminalReasoningEffort.Medium);
        codex.Model_source.Should().Be(TerminalModelSource.Static);
    }

    [Fact(DisplayName = "opencode metadata: модели подставляются из live discovery, эффорт отключён")]
    public async Task Maps_opencode_metadata_from_dynamic_catalog()
    {
        var dynamicCatalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4", "qwen-3"]);
        var dto = await Build([dynamicCatalog]).ToDtoAsync(CancellationToken.None);
        var opencode = dto.Vendors.Single(v => v.Vendor == TerminalAgentVendor.Opencode);

        opencode.Label.Should().Be("OpenCode");
        opencode.Models.Should().Equal("llama-4", "qwen-3");
        opencode.Default_model.Should().Be("llama-4");
        opencode.Supports_effort.Should().BeFalse();
        opencode.Efforts.Should().BeEmpty();
        opencode.Default_effort.Should().BeNull();
        opencode.Model_source.Should().Be(TerminalModelSource.Local);
    }

    [Fact(DisplayName = "opencode metadata: пустой live-список → default_model=null, models=[]")]
    public async Task Maps_opencode_metadata_when_local_endpoint_empty()
    {
        var dynamicCatalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, []);
        var dto = await Build([dynamicCatalog]).ToDtoAsync(CancellationToken.None);
        var opencode = dto.Vendors.Single(v => v.Vendor == TerminalAgentVendor.Opencode);

        opencode.Models.Should().BeEmpty();
        opencode.Default_model.Should().BeNull();
    }

    [Fact(DisplayName = "Capability opencode выключена/undetected → вендор пропадает из списка")]
    public async Task Hides_opencode_when_capability_unavailable()
    {
        var dynamicCatalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);
        var mapper = Build([dynamicCatalog], opencodeAvailable: false);

        var dto = await mapper.ToDtoAsync(CancellationToken.None);

        dto.Vendors.Select(v => v.Vendor)
            .Should().Equal(TerminalAgentVendor.Claude, TerminalAgentVendor.Codex);
    }

    private sealed class StubCatalog(string vendor, IReadOnlyList<string> models) : IVendorModelCatalog
    {
        public string Vendor { get; } = vendor;
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) => Task.FromResult(models);
    }
}
