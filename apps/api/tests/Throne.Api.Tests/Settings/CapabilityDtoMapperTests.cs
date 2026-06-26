using FluentAssertions;
using Throne.Api.Settings;
using Throne.Application.Terminals.Capabilities;
using Throne.Capabilities.Contracts.Generated;
using Throne.Domain.Capabilities;

namespace Throne.Api.Tests.Settings;

public class CapabilityDtoMapperTests
{
    public static IEnumerable<object[]> CatalogNames =>
        CapabilityCatalog.Descriptors.Select(d => new object[] { d.Name });

    // Guards the seam that broke once: a capability added to the catalog/domain but
    // missing from the mapper switches made GET /capabilities throw 500 for everyone.
    [Theory(DisplayName = "ToDto: каждое имя из каталога мапится в контрактный enum без исключения")]
    [MemberData(nameof(CatalogNames))]
    public void Every_catalog_name_maps_to_contract_enum(string domainName)
    {
        var view = new CapabilityView(
            Name: domainName,
            Title: "t",
            Description: "d",
            SelectedProvider: null,
            Providers: []);

        var act = () => CapabilityDtoMapper.ToDto(view);

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "ToDomainName/ParseName: контрактные enum значения round-trip через домен")]
    public void Contract_enum_round_trips_through_domain()
    {
        foreach (var name in Enum.GetValues<CapabilityName>())
        {
            var domain = CapabilityDtoMapper.ToDomainName(name);
            CapabilityNames.IsKnown(domain).Should().BeTrue($"'{domain}' must be a known domain capability");

            var view = new CapabilityView(domain, "t", "d", null, []);
            CapabilityDtoMapper.ToDto(view).Name.Should().Be(name);
        }
    }
}
