using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Realtime;
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
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);
        return services;
    }
}
