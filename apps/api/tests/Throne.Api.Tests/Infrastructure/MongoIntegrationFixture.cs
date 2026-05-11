namespace Throne.Api.Tests.Infrastructure;

[CollectionDefinition(nameof(MongoIntegrationFixture))]
public sealed class MongoIntegrationFixture : ICollectionFixture<MongoFixture>
{
}
