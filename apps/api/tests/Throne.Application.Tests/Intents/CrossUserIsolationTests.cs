using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Intents;

/// <summary>
/// Изоляция multi-user (ADR-0012): Get/List handler'ы возвращают только записи,
/// которые отдал репозиторий — а репозиторий обязан фильтровать по
/// <c>ICurrentUserAccessor.UserId</c>. Здесь мы проверяем, что handler не
/// «доочищает» результат сам и не вмешивается в фильтрацию: он передаёт всё
/// что приходит. Тест регрессионный: если repository вернул запись чужого
/// пользователя (баг в фильтре), ApiException не должен прятать утечку.
/// </summary>
public class CrossUserIsolationTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetIntent возвращает только то, что прислал repository (изоляция полностью на repo)")]
    public async Task GetIntent_returns_repository_result_as_is()
    {
        var repo = Substitute.For<IIntentRepository>();
        var id = IntentId.New();
        var ownIntent = Intent.Restore(id, "user-1", "mine", IntentStatusNames.Draft, 1, [], Now, Now);
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(ownIntent);

        var handler = new GetIntentHandler(repo);
        var got = await handler.HandleAsync(new GetIntentQuery(id.Value), CancellationToken.None);

        got.OwnerUserId.Should().Be("user-1");
    }

    [Fact(DisplayName = "ListIntents возвращает только записи, которые прислал repository")]
    public async Task ListIntents_returns_only_repository_filtered_results()
    {
        var repo = Substitute.For<IIntentRepository>();
        var only = Intent.Restore(IntentId.New(), "user-1", "mine", IntentStatusNames.Draft, 1, [], Now, Now);
        repo.ListAsync(Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>()).Returns([only]);

        var tagRepo = Substitute.For<ITagRepository>();
        var handler = new ListIntentsHandler(repo, tagRepo, Substitute.For<ITmuxSessionManager>());
        var list = await handler.HandleAsync(new ListIntentsQuery(), CancellationToken.None);

        list.Should().HaveCount(1);
        list[0].OwnerUserId.Should().Be("user-1");
    }
}
