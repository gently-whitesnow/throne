using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IIntentRepository, MongoIntentRepository>();
        services.AddSingleton<IInstructionRepository, MongoInstructionRepository>();
        return services;
    }
}
