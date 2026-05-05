using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        var section = configuration.GetSection(AuthOptions.SectionName);
        services.Configure<AuthOptions>(section);

        var options = section.Get<AuthOptions>() ?? new AuthOptions();

        switch (options.Mode)
        {
            case AuthMode.Disabled:
                services.AddSingleton<ICurrentUserAccessor, LocalDevCurrentUserAccessor>();
                break;
            case AuthMode.Jwt:
                throw new NotSupportedException(
                    "Auth:Mode=Jwt is reserved for the auth-gate integration that is not wired up yet. " +
                    "Use Auth:Mode=Disabled for local dev.");
            default:
                throw new InvalidOperationException($"Unknown Auth:Mode '{options.Mode}'.");
        }

        return services;
    }
}
