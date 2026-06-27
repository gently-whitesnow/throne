using Microsoft.AspNetCore.Http.Features;
using Throne.Api.Intents;
using Throne.Api.Realtime;
using Throne.Api.Repositories;
using Throne.Api.Settings;
using Throne.Api.Terminals;
using Throne.Application;
using Throne.Infrastructure;

namespace Throne.Api.Hosting;

public static class ThroneApiCoreServices
{
    public static IServiceCollection AddThroneApiCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddThroneApplication();
        services.AddThroneInfrastructure(configuration);
        services.AddThroneRealtime();
        // Per-endpoint classes + shared IntentsApiHelpers for the four split
        // Intents controllers (IntentsController / IntentPinsController /
        // IntentLinksController / IntentAttachmentsController). Endpoints take
        // ctor deps; Singleton lifetime mirrors the underlying handlers.
        services.AddThroneIntentEndpoints();
        services.AddThroneRepositoryEndpoints();
        services.AddThroneTerminalEndpoints();
        services.AddThroneSettingsEndpoints();
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);
        services.AddBoundedGracefulShutdown();
        return services;
    }
}
