using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;
using DomainCapabilities = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Infrastructure.Tests.Mongo.Capabilities;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoCapabilitiesRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetAsync до первой записи возвращает null")]
    public async Task Get_returns_null_before_first_write()
    {
        var scope = await CapabilitiesRepositoryTestScope.CreateAsync(fixture);

        var result = await scope.Repository.GetAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "SaveAsync upsert'ит singleton-документ и подцепляет toggles")]
    public async Task Save_upserts_singleton_document()
    {
        var scope = await CapabilitiesRepositoryTestScope.CreateAsync(fixture);
        var aggregate = DomainCapabilities.CreateEmpty(Now);
        aggregate.SetEnabled(CapabilityNames.Terminal, true, Now.AddMinutes(1));

        await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SaveAsync(aggregate, ct),
            CancellationToken.None);

        var stored = await scope.Database
            .GetCollection<CapabilitiesDocument>(MongoCollectionNames.Settings)
            .Find(d => d.Id == DomainCapabilities.SingletonId)
            .FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(DomainCapabilities.SingletonId);
        stored.Toggles.Should().ContainKey(CapabilityNames.Terminal).WhoseValue.Should().BeTrue();

        var roundtrip = await scope.Repository.GetAsync(CancellationToken.None);
        roundtrip.Should().NotBeNull();
        roundtrip!.IsEnabled(CapabilityNames.Terminal).Should().BeTrue();
        roundtrip.IsEnabled(CapabilityNames.Vscode).Should().BeFalse();
    }

    [Fact(DisplayName = "Повторный SaveAsync обновляет toggles без создания второго документа")]
    public async Task Save_twice_updates_in_place()
    {
        var scope = await CapabilitiesRepositoryTestScope.CreateAsync(fixture);
        var aggregate = DomainCapabilities.CreateEmpty(Now);
        aggregate.SetEnabled(CapabilityNames.Repositories, true, Now.AddMinutes(1));
        await scope.Uow.ExecuteAsync(ct => scope.Repository.SaveAsync(aggregate, ct), CancellationToken.None);

        aggregate.SetEnabled(CapabilityNames.Repositories, false, Now.AddMinutes(2));
        await scope.Uow.ExecuteAsync(ct => scope.Repository.SaveAsync(aggregate, ct), CancellationToken.None);

        var count = await scope.Database
            .GetCollection<CapabilitiesDocument>(MongoCollectionNames.Settings)
            .CountDocumentsAsync(FilterDefinition<CapabilitiesDocument>.Empty);
        count.Should().Be(1);
        var fetched = await scope.Repository.GetAsync(CancellationToken.None);
        fetched!.IsEnabled(CapabilityNames.Repositories).Should().BeFalse();
        fetched.CurrentVersion.Should().Be(aggregate.CurrentVersion);
    }
}
