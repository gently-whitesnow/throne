using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Infrastructure.Terminals.Capabilities;

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
        if (configuration is not null)
        {
            tmuxBuilder.Bind(configuration.GetSection(TmuxOptions.SectionName));
            capabilitiesBuilder.Bind(configuration.GetSection(CapabilityDetectionOptions.SectionName));
        }

        services.AddSingleton<TmuxCli>();
        services.AddSingleton<ITmuxSessionManager, TmuxSessionManager>();
        services.AddSingleton<ITerminalStreamBridge, TmuxStreamBridge>();

        services.AddSingleton<ICapabilityProbe, TmuxCapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, VsCodeCapabilityProbe>();
        services.AddSingleton<ICapabilityProbe, RepositoriesCapabilityProbe>();
        services.AddSingleton<ICapabilityDetectionCache, CapabilityDetectionCache>();

        return services;
    }
}
