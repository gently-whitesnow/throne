using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Infrastructure.Terminals.Capabilities;
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

        services.AddSingleton<TmuxCli>();
        services.AddSingleton<ITmuxSessionManager, TmuxSessionManager>();
        services.AddSingleton<IClaudeWorkspaceTrust, ClaudeWorkspaceTrust>();
        services.AddSingleton<ITerminalStreamBridge, TmuxStreamBridge>();
        // Application orchestrators consume the bare options instance (see
        // PullRequestSyncBackoff for the same pattern) so Throne.Application
        // does not need a reference to Microsoft.Extensions.Options.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RunPreflightOptions>>().Value);

        services.AddSingleton<ICapabilityProbe, TmuxCapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, VsCodeCapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, RepositoriesCapabilityProbe>();
        services.AddSingleton<ICapabilityDetectionCache, CapabilityDetectionCache>();

        return services;
    }
}
