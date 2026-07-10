using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Throne.Application.Ide;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Infrastructure.Ide;
using Throne.Infrastructure.Terminals.Capabilities;
using Throne.Infrastructure.Terminals.Quotas;
// RunPreflightOptions binding pulls Throne.Application.Terminals into the Infrastructure
// composition root — Application-only DI cannot bind IConfiguration sections directly.

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// DI composition for the embedded-terminal module: tmux shell-out, the WS stream
/// bridge and capability detection probes + TTL cache (ADR-0026 §§ 1-3). All probes
/// share the existing <c>IProcessLauncher</c> registered by the Git module.
///
/// Pulled out of <c>Throne.Infrastructure.DependencyInjection</c> so the root DI
/// file stays under the per-file fan-out budget.
/// </summary>
public static class TerminalsModule
{
    public static IServiceCollection AddThroneTerminalsInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        var tmuxBuilder = services.AddOptions<TmuxOptions>();
        var capabilitiesBuilder = services.AddOptions<CapabilityDetectionOptions>();
        var runPreflightBuilder = services.AddOptions<RunPreflightOptions>();
        if (configuration is not null)
        {
            tmuxBuilder.Bind(configuration.GetSection(TmuxOptions.SectionName));
            capabilitiesBuilder.Bind(configuration.GetSection(CapabilityDetectionOptions.SectionName));
            runPreflightBuilder.Bind(configuration.GetSection(RunPreflightOptions.SectionName));
        }

        runPreflightBuilder.Configure(options =>
            options.ApiBaseUrl = configuration?[SessionHookOptions.ApiBaseUrlKey]
                ?? SessionHookOptions.DefaultApiBaseUrl);

        services.AddSingleton<TmuxCli>();
        services.AddSingleton<ITmuxSessionManager, TmuxSessionManager>();
        services.AddSingleton<IWorkspaceTrustSeeder, ClaudeTrustSeeder>();
        services.AddSingleton<IWorkspaceTrustSeeder, CodexTrustSeeder>();
        services.AddSingleton<IWorkspaceTrust, WorkspaceTrust>();
        services.AddSingleton<ITerminalStreamBridge, TmuxStreamBridge>();
        services.AddHttpClient(OpencodeTuiClient.HttpClientName);
        services.AddSingleton<IOpencodeTuiClient, OpencodeTuiClient>();
        services.AddSingleton<IOpencodeServeGateway, OpencodeServeGateway>();
        services.AddSingleton<ISessionSkillMaterializer, SessionSkillMaterializer>();
        services.AddSingleton(new SessionHookOptions
        {
            ApiBaseUrl = configuration?[SessionHookOptions.ApiBaseUrlKey]
                ?? SessionHookOptions.DefaultApiBaseUrl,
        });
        services.AddSingleton<ISessionHookAdapter, ClaudeSessionHookAdapter>();
        services.AddSingleton<Throne.Application.Ports.ISessionSkillHotAttachWriter, SessionSkillHotAttachWriter>();
        services.AddSingleton<ISessionHookAdapter>(sp =>
            new CodexSessionHookAdapter(
                sp.GetRequiredService<SessionHookOptions>(),
                sp.GetRequiredService<ISessionSkillMaterializer>(),
                CodexSessionProfile.ResolveHome()));
        services.AddSingleton<ISessionHookAdapter, OpencodeSessionHookAdapter>();
        // Application orchestrators consume the bare options instance (see
        // PullRequestSyncBackoff for the same pattern) so Throne.Application
        // does not need a reference to Microsoft.Extensions.Options.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RunPreflightOptions>>().Value);

        services.AddSingleton<ICapabilityProbe, TmuxCapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, VsCodeCapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, CursorCapabilityProbe>();
        services.AddSingleton<ICapabilityDetectionCache, CapabilityDetectionCache>();

        // IDE openers (one per provider key) for the `open_in_ide` carrier
        // capability. Registry resolution is dictionary-based on ProviderName.
        services.AddSingleton<IIdeOpener, VsCodeOpener>();
        services.AddSingleton<IIdeOpener, CursorOpener>();

        services.AddSingleton<WezTermOpener>();
        services.AddSingleton<ITerminalOpener>(sp => sp.GetRequiredService<WezTermOpener>());
        services.AddSingleton<ICapabilityProbe>(sp => sp.GetRequiredService<WezTermOpener>());
        services.AddSingleton<AppleTerminalOpener>();
        services.AddSingleton<ITerminalOpener>(sp => sp.GetRequiredService<AppleTerminalOpener>());
        services.AddSingleton<ICapabilityProbe>(sp => sp.GetRequiredService<AppleTerminalOpener>());

        // Per-vendor login probes feed the vendor-card status and the readiness check
        // (`GET /terminal/vendors` → login_status). opencode has no probe — it is surfaced
        // statically as `in_development` by the catalog mapper.
        services.AddSingleton<IAgentVendorLoginProbe, ClaudeLoginProbe>();
        services.AddSingleton<IAgentVendorLoginProbe, CodexLoginProbe>();

        // Per-vendor Pro/Max quota probes (ADR-0054). Best-effort — the base class
        // swallows any error into a null snapshot so a broken adapter never blocks Run.
        services.AddHttpClient(ClaudeQuotaAdapter.HttpClientName);
        services.AddHttpClient(CodexQuotaAdapter.HttpClientName);
        services.AddSingleton<IVendorQuotaAdapter, ClaudeQuotaAdapter>();
        services.AddSingleton<IVendorQuotaAdapter, CodexQuotaAdapter>();

        return services;
    }
}
