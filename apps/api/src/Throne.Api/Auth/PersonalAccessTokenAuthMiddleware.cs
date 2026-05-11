using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Throne.Application.Auth;

namespace Throne.Api.Auth;

/// <summary>
/// Корневая аутентификация по <c>Authorization: Bearer &lt;token&gt;</c> (ADR-0012, ADR-0016).
///
/// Регистрируется на корне приложения, чтобы и MCP-канал, и REST-эндпоинты
/// (например <c>/api/v1/chat-uploads</c>, к которому обращается локальный CLI)
/// могли принять Personal Access Token.
///
/// - Под <c>Auth:Mode=Disabled</c> — no-op: <c>LocalDevCurrentUserAccessor</c>
///   подменяет userId на <c>local-dev</c>.
/// - Под <c>Auth:Mode=Jwt</c>:
///   - Если запрос уже аутентифицирован (cookie/JWT через <c>UseAuthentication</c>) —
///     middleware не вмешивается.
///   - Если в заголовке (или fallback <c>?token=</c>) лежит Bearer:
///     - **JWT-shaped** (три base64url-сегмента) — пропускается дальше; считаем,
///       что <c>JwtBearer</c>-pipeline уже его обработал. Если он тем не менее
///       не аутентифицировал контекст — это нелегитимный JWT, ничего не делаем
///       (защита эндпоинта остаётся за <c>[Authorize]</c> или ветка /mcp).
///     - **Иначе** — резолвим как PAT и подменяем <see cref="ClaimsPrincipal"/>.
///   - Если Bearer нет — middleware пропускает запрос дальше, чтобы анонимные
///     эндпоинты (health, swagger, OAuth metadata) продолжали работать.
///
/// 401 на <c>/mcp</c> пишет <see cref="McpRequiresBearerMiddleware"/> — он добавляет
/// <c>WWW-Authenticate: Bearer …, resource_metadata="…"</c> по RFC 9728.
/// </summary>
public sealed class PersonalAccessTokenAuthMiddleware(
    RequestDelegate next,
    IPersonalAccessTokenResolver resolver,
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

        var token = BearerTokenExtractor.Extract(context.Request);
        if (string.IsNullOrEmpty(token))
        {
            await next(context);
            return;
        }

        if (JwtShapeDetector.LooksLikeJwt(token))
        {
            // JwtBearer уже отработал в UseAuthentication() — если контекст
            // не аутентифицирован, токен невалидный. Пропускаем дальше; запрет
            // конкретному эндпоинту обеспечит [Authorize] или ветка /mcp.
            await next(context);
            return;
        }

        var ownerUserId = await resolver.ResolveOwnerUserIdAsync(token, context.RequestAborted);
        if (string.IsNullOrEmpty(ownerUserId))
        {
            await next(context);
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
}

public static class ThroneAuthMiddlewareExtensions
{
    public static IApplicationBuilder UsePersonalAccessTokenAuth(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<PersonalAccessTokenAuthMiddleware>();
    }

    public static IApplicationBuilder UseMcpRequiresBearer(this IApplicationBuilder app, PathString path)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments(path),
            branch => branch.UseMiddleware<McpRequiresBearerMiddleware>());
    }
}
