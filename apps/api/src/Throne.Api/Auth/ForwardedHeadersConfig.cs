using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Throne.Api.Auth;

/// <summary>
/// Caddy/nginx терминируют TLS снаружи и шлют <c>X-Forwarded-Proto</c> в throne-api.
/// По умолчанию <see cref="ForwardedHeadersMiddleware"/> доверяет этим заголовкам
/// только от loopback — в docker-сети прокси на отдельном IP, поэтому очищаем
/// known networks/proxies. throne-api не экспонирован наружу, только через
/// reverse-proxy (см. throne-infra/compose.yml + Caddyfile).
///
/// Без этого <c>HttpRequest.Scheme</c> равен <c>"http"</c> даже на HTTPS-запросе,
/// что ломает RFC 9728 metadata (resource URL должен быть <c>https://</c>) и
/// <c>resource_metadata</c> в <c>WWW-Authenticate</c>.
/// </summary>
public static class ForwardedHeadersConfig
{
    public static IServiceCollection AddTrustedReverseProxyForwarding(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // IStartupFilter автоматически вставляет UseForwardedHeaders в pipeline
        // первым — Program.cs не зовёт никаких Use*, и coupling в Main падает
        // ниже CA1506 threshold (HttpOverrides перестаёт там фигурировать).
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, ForwardedHeadersStartupFilter>());
        return services;
    }

    private sealed class ForwardedHeadersStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.UseForwardedHeaders();
                next(app);
            };
    }
}
