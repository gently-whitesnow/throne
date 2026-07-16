using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.GenericHttp;
using Throne.Infrastructure.TaskTrackers.Kaiten;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public class KaitenTaskTrackerRegistrationTests
{
    [Fact(DisplayName = "Провайдер несёт открытый ключ 'kaiten' и читаемое имя")]
    public void Provider_identity()
    {
        // Identity (key + label) is independent of the HTTP client, so a null client suffices here.
        var provider = new KaitenTaskTrackerProvider(client: null!);
        provider.TrackerKey.Should().Be("kaiten");
        provider.DisplayName.Should().Be("Kaiten");
    }

    [Fact(DisplayName = "Модуль регистрирует Kaiten как ITaskTrackerProvider и собирает HTTP-клиент")]
    public void Module_registers_provider_and_client()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        TaskTrackerInfrastructureModule.AddThroneTaskTrackerInfrastructure(services, configuration: null);

        using var provider = services.BuildServiceProvider();

        provider.GetServices<ITaskTrackerProvider>()
            .Select(p => p.TrackerKey)
            .Should().ContainInOrder("kaiten", "custom-http");

        var client = provider.GetRequiredService<IKaitenClient>();
        client.Cards.Should().NotBeNull();
        client.CardChildren.Should().NotBeNull();
        client.Topology.Should().NotBeNull();
        provider.GetRequiredService<GenericHttpClient>().Should().NotBeNull();
    }
}
