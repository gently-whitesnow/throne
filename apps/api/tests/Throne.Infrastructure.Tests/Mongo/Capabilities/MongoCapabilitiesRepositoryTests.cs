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

    [Fact(DisplayName = "GetAsync returns null before first write")]
    public async Task Get_returns_null_before_first_write()
    {
        var scope = await CapabilitiesRepositoryTestScope.CreateAsync(fixture);

        var result = await scope.Repository.GetAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "SaveAsync upserts singleton with selections map")]
    public async Task Save_upserts_singleton_document()
    {
        var scope = await CapabilitiesRepositoryTestScope.CreateAsync(fixture);
        var aggregate = DomainCapabilities.CreateEmpty(Now);
        aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "vscode", Now.AddMinutes(1));

        await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SaveAsync(aggregate, ct),
            CancellationToken.None);

        var stored = await scope.Database
            .GetCollection<CapabilitiesDocument>(MongoCollectionNames.Settings)
            .Find(d => d.Id == DomainCapabilities.SingletonId)
            .FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(DomainCapabilities.SingletonId);
        stored.Selections.Should().ContainKey(CapabilityNames.OpenInIde).WhoseValue.Should().Be("vscode");

        var roundtrip = await scope.Repository.GetAsync(CancellationToken.None);
        roundtrip.Should().NotBeNull();
        roundtrip!.GetSelectedProvider(CapabilityNames.OpenInIde).Should().Be("vscode");
    }

    [Fact(DisplayName = "Second SaveAsync updates selection in place without creating duplicate documents")]
    public async Task Save_twice_updates_in_place()
    {
        var scope = await CapabilitiesRepositoryTestScope.CreateAsync(fixture);
        var aggregate = DomainCapabilities.CreateEmpty(Now);
        aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "vscode", Now.AddMinutes(1));
        await scope.Uow.ExecuteAsync(ct => scope.Repository.SaveAsync(aggregate, ct), CancellationToken.None);

        aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "cursor", Now.AddMinutes(2));
        await scope.Uow.ExecuteAsync(ct => scope.Repository.SaveAsync(aggregate, ct), CancellationToken.None);

        var count = await scope.Database
            .GetCollection<CapabilitiesDocument>(MongoCollectionNames.Settings)
            .CountDocumentsAsync(FilterDefinition<CapabilitiesDocument>.Empty);
        count.Should().Be(1);
        var fetched = await scope.Repository.GetAsync(CancellationToken.None);
        fetched!.GetSelectedProvider(CapabilityNames.OpenInIde).Should().Be("cursor");
        fetched.CurrentVersion.Should().Be(aggregate.CurrentVersion);
    }
}
