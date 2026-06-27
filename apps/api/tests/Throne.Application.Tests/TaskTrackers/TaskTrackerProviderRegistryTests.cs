using FluentAssertions;
using Throne.Application.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers;

public class TaskTrackerProviderRegistryTests
{
    private sealed record FakeProvider(string TrackerKey, string DisplayName) : ITaskTrackerProvider;

    [Fact(DisplayName = "Пустой реестр (provider-neutral core) загружается и не отдаёт провайдеров")]
    public void Empty_registry_boots_with_no_providers()
    {
        var registry = new TaskTrackerProviderRegistry([]);

        registry.AllProviders.Should().BeEmpty();
        registry.GetByName("kaiten").Should().BeNull();
    }

    [Fact(DisplayName = "AllProviders сохраняет порядок регистрации")]
    public void Preserves_registration_order()
    {
        var registry = new TaskTrackerProviderRegistry(
        [
            new FakeProvider("kaiten", "Kaiten"),
            new FakeProvider("jira", "Jira"),
        ]);

        registry.AllProviders.Select(p => p.TrackerKey).Should().Equal("kaiten", "jira");
    }

    [Fact(DisplayName = "GetByName резолвит зарегистрированный ключ и null для неизвестного")]
    public void Resolves_known_and_misses_unknown()
    {
        var kaiten = new FakeProvider("kaiten", "Kaiten");
        var registry = new TaskTrackerProviderRegistry([kaiten]);

        registry.GetByName("kaiten").Should().BeSameAs(kaiten);
        registry.GetByName("linear").Should().BeNull();
    }

    [Fact(DisplayName = "Дублирующая регистрация одного ключа падает на старте")]
    public void Duplicate_key_throws()
    {
        var act = () => new TaskTrackerProviderRegistry(
        [
            new FakeProvider("kaiten", "Kaiten"),
            new FakeProvider("kaiten", "Kaiten Duplicate"),
        ]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*kaiten*");
    }
}
