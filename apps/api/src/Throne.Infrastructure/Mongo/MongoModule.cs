using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Mongo-секция корневого DI-композита: options + клиент с тайм-аутами/pool из
/// <see cref="MongoClientOptions"/>, SDAM-подпиской через <c>ClusterConfigurator</c>
/// Вынесено из <see cref="DependencyInjection"/>, чтобы не наращивать fan-out/LOC
/// корневого метода.
/// </summary>
internal static class MongoModule
{
    public static IServiceCollection AddThroneMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName));
        services.AddOptions<MongoClientOptions>()
            .Bind(configuration.GetSection(MongoClientOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var mongo = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            var client = sp.GetRequiredService<IOptions<MongoClientOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Throne.Infrastructure.Mongo");
            var settings = MongoClientFactory.BuildSettings(mongo, client, logger);
            return new MongoClient(settings);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(opts.Database);
        });

        return services;
    }
}
