using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Auth;
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
        services.AddThroneAuth(configuration);
        services.AddThroneRealtime();
        services.AddThroneTools();
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);
        // ForwardedHeaders + IStartupFilter: throne-api за Caddy/nginx, без этого
        // HttpRequest.Scheme = "http" даже на HTTPS-запросе → ломает RFC 9728
        // metadata и resource_metadata в WWW-Authenticate (см. ForwardedHeadersConfig).
        services.AddTrustedReverseProxyForwarding();
        return services;
    }
}
