using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Intents;

namespace Throne.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateIntentHandler>();
        services.AddScoped<GetIntentHandler>();
        services.AddScoped<ReadIntentTextHandler>();
        services.AddScoped<ReplaceIntentTextHandler>();
        return services;
    }
}
