using FluentAssertions;
using Throne.Api.Terminals;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Tests.Terminals;

public class TerminalVendorCatalogMapperTests
{
    private static TerminalVendorCatalogMapper Build(
        IVendorModelCatalog[]? dynamicCatalogs = null,
        AgentVendorLoginStatus claudeLogin = AgentVendorLoginStatus.Ready,
        AgentVendorLoginStatus codexLogin = AgentVendorLoginStatus.LoggedOut)
    {
        IAgentVendorLoginProbe[] probes =
        [
            new StubLoginProbe(TerminalAgentCatalog.VendorClaude, claudeLogin),
            new StubLoginProbe(TerminalAgentCatalog.VendorCodex, codexLogin),
        ];
        var catalog = new TerminalVendorCatalog(
        [
            TerminalVendorDescriptors.Claude,
            TerminalVendorDescriptors.Codex,
            TerminalVendorDescriptors.Opencode,
        ]);
        return new TerminalVendorCatalogMapper(
            catalog,
            dynamicCatalogs ?? Array.Empty<IVendorModelCatalog>(),
            probes);
    }

    [Fact(DisplayName = "Каталог: default_vendor=claude и все три вендора отданы в порядке каталога")]
    public async Task Maps_default_vendor_and_vendor_order()
    {
        var dto = await Build().ToDtoAsync(CancellationToken.None);

        dto.Default_vendor.Should().Be(TerminalAgentCatalog.VendorClaude);
        dto.Vendors.Select(v => v.Vendor)
            .Should().Equal(TerminalAgentCatalog.VendorClaude, TerminalAgentCatalog.VendorCodex, TerminalAgentCatalog.VendorOpencode);
    }

    [Fact(DisplayName = "claude metadata: модели opus-first, дефолт opus, эффорт high, источник static")]
    public async Task Maps_claude_metadata()
    {
        var dto = await Build().ToDtoAsync(CancellationToken.None);
        var claude = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorClaude);

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
        var codex = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorCodex);

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
        var opencode = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorOpencode);

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
        var opencode = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorOpencode);

        opencode.Models.Should().BeEmpty();
        opencode.Default_model.Should().BeNull();
    }

    [Fact(DisplayName = "login_status: проба вендора отражается, claude=ready/codex=logged_out, оба selectable")]
    public async Task Maps_vendor_login_status()
    {
        var dto = await Build(
            claudeLogin: AgentVendorLoginStatus.Ready,
            codexLogin: AgentVendorLoginStatus.LoggedOut).ToDtoAsync(CancellationToken.None);

        var claude = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorClaude);
        claude.Login_status.Should().Be(TerminalVendorLoginStatus.Ready);
        claude.Selectable.Should().BeTrue();

        var codex = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorCodex);
        codex.Login_status.Should().Be(TerminalVendorLoginStatus.Logged_out);
        codex.Selectable.Should().BeTrue();
    }

    [Fact(DisplayName = "login_status: claude CLI отсутствует → missing, но всё ещё selectable")]
    public async Task Maps_missing_cli_login_status()
    {
        var dto = await Build(claudeLogin: AgentVendorLoginStatus.Missing).ToDtoAsync(CancellationToken.None);
        var claude = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorClaude);

        claude.Login_status.Should().Be(TerminalVendorLoginStatus.Missing);
        claude.Selectable.Should().BeTrue();
    }

    [Fact(DisplayName = "opencode: помечен как in_development и не selectable")]
    public async Task Opencode_is_in_development_and_not_selectable()
    {
        var dynamicCatalog = new StubCatalog(TerminalAgentCatalog.VendorOpencode, ["llama-4"]);

        var dto = await Build([dynamicCatalog]).ToDtoAsync(CancellationToken.None);
        var opencode = dto.Vendors.Single(v => v.Vendor == TerminalAgentCatalog.VendorOpencode);

        opencode.Login_status.Should().Be(TerminalVendorLoginStatus.In_development);
        opencode.Selectable.Should().BeFalse();
        opencode.Login_detail.Should().Be("в разработке");
    }

    private sealed class StubCatalog(string vendor, IReadOnlyList<string> models) : IVendorModelCatalog
    {
        public string Vendor { get; } = vendor;
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) => Task.FromResult(models);
    }

    private sealed class StubLoginProbe(string vendor, AgentVendorLoginStatus status) : IAgentVendorLoginProbe
    {
        public string Vendor { get; } = vendor;
        public Task<AgentVendorLoginResult> ProbeAsync(CancellationToken ct) =>
            Task.FromResult(new AgentVendorLoginResult(status, Detail: null));
    }
}
