using System.Net;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Собирает <see cref="MongoClientSettings"/> из connection string + накладывает
/// тайм-ауты/pool-настройки из <see cref="MongoClientOptions"/>, и подписывается на
/// SDAM-события через <c>ClusterConfigurator</c>. После инцидента 2026-06-17 без
/// этих подписок «mongo на 5 секунд провалилась и встала» был виден только косвенно —
/// по падающим воркерам; теперь heartbeat-провал/восстановление и смена топологии
/// логируются отдельным сигналом.
/// </summary>
internal static partial class MongoClientFactory
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Mongo heartbeat failed: server={Endpoint}, duration_ms={DurationMs}.")]
    private static partial void LogHeartbeatFailed(ILogger logger, Exception ex, EndPoint endpoint, double durationMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Mongo heartbeat ok: server={Endpoint}, duration_ms={DurationMs}.")]
    private static partial void LogHeartbeatOk(ILogger logger, EndPoint endpoint, double durationMs);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Mongo server state changed: server={Endpoint}, {OldState} → {NewState}.")]
    private static partial void LogStateChanged(ILogger logger, EndPoint endpoint, ServerState oldState, ServerState newState);

    public static MongoClientSettings BuildSettings(MongoOptions mongo, MongoClientOptions client, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(mongo);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        var settings = MongoClientSettings.FromConnectionString(mongo.ConnectionString);

        settings.ApplicationName = client.ApplicationName;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(client.ServerSelectionTimeoutSeconds);
        settings.ConnectTimeout = TimeSpan.FromSeconds(client.ConnectTimeoutSeconds);
        settings.SocketTimeout = TimeSpan.FromSeconds(client.SocketTimeoutSeconds);
        settings.HeartbeatInterval = TimeSpan.FromSeconds(client.HeartbeatFrequencySeconds);
        settings.MaxConnectionPoolSize = client.MaxConnectionPoolSize;
        settings.MinConnectionPoolSize = client.MinConnectionPoolSize;
        settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(client.MaxConnectionIdleMinutes);
        settings.MaxConnectionLifeTime = TimeSpan.FromMinutes(client.MaxConnectionLifeMinutes);
        settings.WaitQueueTimeout = TimeSpan.FromSeconds(client.WaitQueueTimeoutSeconds);

        settings.ClusterConfigurator = cb => SubscribeSdamEvents(cb, logger);

        return settings;
    }

    private static void SubscribeSdamEvents(MongoDB.Driver.Core.Configuration.ClusterBuilder cb, ILogger logger)
    {
        cb.Subscribe<ServerHeartbeatFailedEvent>(e =>
            LogHeartbeatFailed(logger, e.Exception, e.ConnectionId.ServerId.EndPoint, e.Duration.TotalMilliseconds));

        cb.Subscribe<ServerHeartbeatSucceededEvent>(e =>
            LogHeartbeatOk(logger, e.ConnectionId.ServerId.EndPoint, e.Duration.TotalMilliseconds));

        cb.Subscribe<ServerDescriptionChangedEvent>(e =>
            LogStateChanged(logger, e.NewDescription.EndPoint, e.OldDescription.State, e.NewDescription.State));
    }
}
