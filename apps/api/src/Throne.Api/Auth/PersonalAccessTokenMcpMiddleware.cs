using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Throne.Application.Auth;

namespace Throne.Api.Auth;

/// <summary>
/// Авторизация запросов к MCP-транспорту через Personal Access Token.
///
/// - Под <c>Auth:Mode=Disabled</c> middleware — no-op: <c>LocalDevCurrentUserAccessor</c>
///   подменяет userId на <c>local-dev</c>.
/// - Под <c>Auth:Mode=Jwt</c> middleware читает <c>Authorization: Bearer &lt;token&gt;</c>
///   (или fallback <c>?token=</c>) и резолвит plaintext PAT в userId. На успехе
///   подмешивает <see cref="ClaimsPrincipal"/> с claim <c>user_id</c>, который потом
///   считывает <see cref="HttpContextCurrentUserAccessor"/>. На отсутствии или
///   невалидном токене — 401.
/// </summary>
public sealed class PersonalAccessTokenMcpMiddleware(
    RequestDelegate next,
    IPersonalAccessTokenResolver resolver,
    IOptionsMonitor<AuthOptions> options)
{
    private const string BearerPrefix = "Bearer ";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (options.CurrentValue.Mode != AuthMode.Jwt)
        {
            await next(context);
            return;
        }

        var token = ExtractToken(context.Request);
        if (string.IsNullOrEmpty(token))
        {
            await Write401Async(context, "Missing PAT.");
            return;
        }

        var ownerUserId = await resolver
            .ResolveOwnerUserIdAsync(token, context.RequestAborted)
            ;

        if (string.IsNullOrEmpty(ownerUserId))
        {
            await Write401Async(context, "Invalid PAT.");
            return;
        }

        var identity = new ClaimsIdentity(
            claims: [new Claim(AuthOptions.UserIdClaim, ownerUserId)],
            authenticationType: "PAT",
            nameType: AuthOptions.UserIdClaim,
            roleType: ClaimTypes.Role);
        context.User = new ClaimsPrincipal(identity);

        await next(context);
    }

    private static string? ExtractToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Authorization", out var auth) && auth.Count > 0)
        {
            var raw = auth[0];
            if (!string.IsNullOrEmpty(raw) && raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = raw[BearerPrefix.Length..].Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        if (request.Query.TryGetValue("token", out var qs) && qs.Count > 0)
        {
            var value = qs[0];
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static Task Write401Async(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer realm=\"throne-mcp\"";
        return context.Response.WriteAsync(detail, context.RequestAborted);
    }
}

public static class PersonalAccessTokenMcpMiddlewareExtensions
{
    public static IApplicationBuilder UsePersonalAccessTokenMcpAuth(
        this IApplicationBuilder app,
        PathString path)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments(path),
            branch => branch.UseMiddleware<PersonalAccessTokenMcpMiddleware>());
    }
}
