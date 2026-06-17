using FluentAssertions;
using MongoDB.Driver;
using Polly.Timeout;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

public class MongoResilienceTests
{
    [Theory(DisplayName = "IsMongoTransient распознаёт связность/таймауты как транзиентные")]
    [MemberData(nameof(TransientCases))]
    public void Transient_exceptions_are_classified_as_retryable(Exception ex)
    {
        MongoResilience.IsMongoTransient(ex).Should().BeTrue();
    }

    [Fact(DisplayName = "Прикладное исключение не считается транзиентным")]
    public void Domain_exception_is_not_transient()
    {
        MongoResilience.IsMongoTransient(new InvalidOperationException("boom")).Should().BeFalse();
    }

    [Fact(DisplayName = "null не считается транзиентным")]
    public void Null_is_not_transient()
    {
        MongoResilience.IsMongoTransient(null).Should().BeFalse();
    }

    public static TheoryData<Exception> TransientCases() => new()
    {
        new TimeoutException("server selection timeout"),
        new MongoConnectionException(
            new MongoDB.Driver.Core.Connections.ConnectionId(
                new MongoDB.Driver.Core.Servers.ServerId(
                    new MongoDB.Driver.Core.Clusters.ClusterId(1),
                    new System.Net.DnsEndPoint("localhost", 27017))),
            "heartbeat failed"),
        new TimeoutRejectedException("polly timeout"),
    };
}
