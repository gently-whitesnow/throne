using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Hosting;
using Throne.Api.Intents;
using Throne.Api.Realtime;
using Throne.Api.Repositories;
using Throne.Api.Terminals;
using Throne.Application;
using Throne.Infrastructure;

namespace Throne.Api.Mcp;

public static class ThroneMcpCoreServices
{
    public static IServiceCollection AddThroneMcpCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddThroneApplication();
        services.AddThroneInfrastructure(configuration);
        services.AddThroneRealtime();
        services.AddThroneTools();
        // Per-endpoint classes + shared IntentsApiHelpers for the four split
        // Intents controllers (IntentsController / IntentPinsController /
        // IntentLinksController / IntentAttachmentsController). Endpoints take
        // ctor deps; Singleton lifetime mirrors the underlying handlers.
        services.AddThroneIntentEndpoints();
        services.AddThroneRepositoryEndpoints();
        services.AddThroneTerminalEndpoints();
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);
        // Bound the graceful-shutdown backstop (default 30s → 10s); long-lived
        // connections still unwind promptly on ApplicationStopping themselves.
        services.AddBoundedGracefulShutdown();
        return services;
    }
}
