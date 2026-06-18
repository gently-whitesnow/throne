using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

public class MongoClientFactoryTests
{
    [Fact(DisplayName = "BuildSettings накладывает оверрайды из MongoClientOptions поверх connection string")]
    public void Overrides_applied_over_connection_string()
    {
        var mongo = new MongoOptions
        {
            ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0",
            Database = "throne",
        };
        var client = new MongoClientOptions
        {
            ApplicationName = "throne-test",
            ServerSelectionTimeoutSeconds = 7,
            ConnectTimeoutSeconds = 11,
            SocketTimeoutSeconds = 33,
            HeartbeatFrequencySeconds = 4,
            MaxConnectionPoolSize = 123,
            MinConnectionPoolSize = 5,
            MaxConnectionIdleMinutes = 6,
            MaxConnectionLifeMinutes = 17,
            WaitQueueTimeoutSeconds = 12,
        };

        var settings = MongoClientFactory.BuildSettings(mongo, client, NullLogger.Instance);

        settings.ApplicationName.Should().Be("throne-test");
        settings.ServerSelectionTimeout.Should().Be(TimeSpan.FromSeconds(7));
        settings.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(11));
        settings.SocketTimeout.Should().Be(TimeSpan.FromSeconds(33));
        settings.HeartbeatInterval.Should().Be(TimeSpan.FromSeconds(4));
        settings.MaxConnectionPoolSize.Should().Be(123);
        settings.MinConnectionPoolSize.Should().Be(5);
        settings.MaxConnectionIdleTime.Should().Be(TimeSpan.FromMinutes(6));
        settings.MaxConnectionLifeTime.Should().Be(TimeSpan.FromMinutes(17));
        settings.WaitQueueTimeout.Should().Be(TimeSpan.FromSeconds(12));
        settings.ClusterConfigurator.Should().NotBeNull("ClusterConfigurator должен быть выставлен для SDAM-подписок");
    }

    [Fact(DisplayName = "Дефолтная connection string не содержит directConnection=true")]
    public void Default_connection_string_has_no_direct_connection()
    {
        var mongo = new MongoOptions();

        mongo.ConnectionString
            .Should().NotContain("directConnection=true",
                "directConnection=true отключает SDAM-failover и роняет весь клиент при первом heartbeat-провале (см. инцидент 2026-06-17)");
    }
}
