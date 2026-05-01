using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddThroneTools();
        return services;
    }
}
