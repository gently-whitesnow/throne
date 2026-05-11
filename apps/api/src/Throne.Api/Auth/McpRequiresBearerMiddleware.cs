using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Throne.Application.Auth;

namespace Throne.Api.Auth;

/// <summary>
/// Гейт <c>/mcp</c> ветви: под <c>Auth:Mode=Jwt</c> запросы без валидного
/// Bearer-токена получают 401 с заголовком <c>WWW-Authenticate</c> по RFC 9728.
/// Бoльшая часть REST-эндпоинтов закрыта <c>[Authorize]</c>, а <c>MapMcp</c>
/// помечен <c>AllowAnonymous</c> — там 401 с metadata-discovery и пишет этот
/// middleware.
/// </summary>
public sealed class McpRequiresBearerMiddleware(
    RequestDelegate next,
    IOptionsMonitor<AuthOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (options.CurrentValue.Mode != AuthMode.Jwt)
        {
            await next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        var hadBearer = !string.IsNullOrEmpty(BearerTokenExtractor.Extract(context.Request));
        var detail = hadBearer ? "Invalid bearer token." : "Missing bearer token.";
        await Write401Async(context, detail);
    }

    private static Task Write401Async(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        // RFC 9728 §5.1: resource_metadata указывает URL Protected Resource Metadata,
        // через который клиент (Claude Desktop Custom Connector и т.п.) узнаёт об
        // Authorization Server. Без этого параметра auto-discovery не работает.
        string metadataUrl = $"{context.Request.Scheme}://{context.Request.Host}/.well-known/oauth-protected-resource";
        context.Response.Headers.WWWAuthenticate =
            $"Bearer realm=\"throne-mcp\", resource_metadata=\"{metadataUrl}\", error=\"invalid_token\", error_description=\"{detail}\"";
        return context.Response.WriteAsync(detail, context.RequestAborted);
    }
}

