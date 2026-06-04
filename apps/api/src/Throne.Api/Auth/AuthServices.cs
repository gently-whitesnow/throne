using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Throne.Application.Auth;

namespace Throne.Api.Auth;

public static class AuthServices
{
    public static IServiceCollection AddThroneAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<AuthAudienceOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddHttpContextAccessor();

        // Singleton: обе реализации stateless. HttpContextCurrentUserAccessor читает
        // пользователя текущего запроса через AsyncLocal-backed IHttpContextAccessor,
        // LocalDevCurrentUserAccessor отдаёт константу. AuthMode фиксируется на старте,
        // поэтому ветка фабрики стабильна. Singleton избегает captive dependency: весь
        // репо-слой (и singleton MCP-инструменты/эндпоинты) потребляет этот accessor, и
        // Scoped-регистрация ломала ValidateOnBuild в Development (захват scoped из singleton).
        services.AddSingleton<ICurrentUserAccessor>(sp =>
        {
            var mode = sp.GetRequiredService<IOptions<AuthOptions>>().Value.Mode;
            return mode switch
            {
                AuthMode.Jwt => ActivatorUtilities.CreateInstance<HttpContextCurrentUserAccessor>(sp),
                AuthMode.Disabled => new LocalDevCurrentUserAccessor(),
                _ => throw new InvalidOperationException($"Unknown Auth:Mode '{mode}'."),
            };
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
        services.AddSingleton<IPostConfigureOptions<AuthorizationOptions>, ConfigureAuthorization>();
        services.AddAuthorization();

        return services;
    }

    private sealed class ConfigureJwtBearer(
        IOptions<AuthOptions> authOptions,
        IOptions<AuthAudienceOptions> audienceOptions)
        : IConfigureNamedOptions<JwtBearerOptions>
    {
        public void Configure(JwtBearerOptions options)
            => Configure(JwtBearerDefaults.AuthenticationScheme, options);

        public void Configure(string? name, JwtBearerOptions o)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
            {
                return;
            }

            var auth = authOptions.Value;
            var audience = audienceOptions.Value;

            if (!string.IsNullOrWhiteSpace(auth.Authority))
            {
                o.Authority = auth.Authority;
            }

            if (!string.IsNullOrWhiteSpace(auth.MetadataAddress))
            {
                o.MetadataAddress = auth.MetadataAddress;
            }

            o.RequireHttpsMetadata = auth.RequireHttpsMetadata;
            o.MapInboundClaims = false;

            // Допустимые audience: основной (REST) + дополнительные (OAuth resource URLs, ADR-0016).
            string[] audiences = (audience.AdditionalAudiences ?? new())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Concat(string.IsNullOrWhiteSpace(audience.Audience) ? Array.Empty<string>() : new[] { audience.Audience })
                .ToArray();

            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(audience.Issuer),
                ValidIssuer = audience.Issuer,
                ValidateAudience = audiences.Length > 0,
                ValidAudiences = audiences,
                ValidateLifetime = true,
                NameClaimType = AuthOptions.UserIdClaim,
            };
        }
    }

    private sealed class ConfigureAuthorization(IOptions<AuthOptions> options)
        : IPostConfigureOptions<AuthorizationOptions>
    {
        public void PostConfigure(string? name, AuthorizationOptions o)
        {
            if (options.Value.Mode == AuthMode.Jwt)
            {
                o.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        }
    }
}
