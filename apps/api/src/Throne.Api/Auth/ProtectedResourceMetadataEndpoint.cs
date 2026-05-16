using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Throne.Api.Auth;

/// <summary>
/// RFC 9728 — OAuth 2.0 Protected Resource Metadata.
/// Throne-api публикует <c>/.well-known/oauth-protected-resource</c>, чтобы
/// MCP-клиент (например, Claude Desktop Custom Connector) мог обнаружить
/// связанный внешний Authorization Server без зашитых URL (ADR-0016).
/// Конкретная реализация AS — деталь деплоя; throne общается с ней только
/// через JWKS-валидацию входящих токенов.
///
/// Контент собирается из <see cref="AuthOptions"/>:
///   - <c>resource</c> — URL текущего MCP-эндпоинта (берётся из заголовков
///     запроса; см. ниже).
///   - <c>authorization_servers</c> — issuer из <see cref="AuthOptions.Authority"/>.
///   - <c>scopes_supported</c> — единственный <c>mcp:full</c> в MVP.
///   - <c>bearer_methods_supported</c> — только <c>header</c> (Bearer в заголовке).
/// </summary>
public static class ProtectedResourceMetadataEndpoint
{
    private static readonly string[] BearerMethods = ["header"];
    private static readonly string[] Scopes = ["mcp:full"];

    public static IEndpointRouteBuilder MapOAuthProtectedResource(
        this IEndpointRouteBuilder endpoints,
        string resourcePath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/.well-known/oauth-protected-resource", (HttpContext ctx, IOptions<AuthOptions> options) =>
        {
            AuthOptions auth = options.Value;
            string scheme = ctx.Request.Scheme;
            string host = ctx.Request.Host.ToString();
            string resource = $"{scheme}://{host}{resourcePath}";

            string[] authorizationServers = !string.IsNullOrWhiteSpace(auth.Authority)
                ? [auth.Authority.TrimEnd('/')]
                : Array.Empty<string>();

            return Results.Json(new
            {
                resource,
                authorization_servers = authorizationServers,
                scopes_supported = Scopes,
                bearer_methods_supported = BearerMethods,
            });
        }).AllowAnonymous();

        return endpoints;
    }
}
