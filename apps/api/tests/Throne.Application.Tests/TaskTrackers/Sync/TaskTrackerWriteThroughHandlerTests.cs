using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers.Sync;

public class TaskTrackerWriteThroughHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeCardLinkStore _links = new();
    private readonly FakeConnectionStore _connections = new();
    private readonly RecordingSyncProvider _provider = new();

    public TaskTrackerWriteThroughHandlerTests() =>
        _connections.Seed("kaiten", new TaskTrackerStoredConnection("https://acme.kaiten.ru", "tok", []));

    private TaskTrackerWriteThroughHandler Handler() =>
        new(_links, _connections, [_provider], new FakeTimeProvider(Now),
            NullLogger<TaskTrackerWriteThroughHandler>.Instance);

    private static CardSyncLink Linked(string intentId, string cardId, CardSyncLinkSnapshot snapshot) =>
        CardSyncLink.Create(intentId, new TaskTrackerCardLink("kaiten", "board-7", cardId), snapshot, CardSyncLinkCursors.Empty, Now);

    private static Intent IntentWith(string id, string text, string? title = null) =>
        Intent.Create(new IntentId(id), text, null, Now, title: title);

    [Fact(DisplayName = "IntentTextChanged: расхождение со снапшотом пушит только description и двигает его в снапшоте")]
    public async Task Text_changed_pushes_description_only()
    {
        _links.Seed(Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", "Old", "col", "Todo")));

        await Handler().HandleAsync(
            new IntentTextChanged(IntentWith("intent-1", "New body", title: "Title")), CancellationToken.None);

        _provider.Updated.Should().ContainSingle();
        var (cardId, patch) = _provider.Updated[0];
        cardId.Should().Be("card-42");
        patch.Title.Should().BeNull();
        patch.Description.Should().Be("New body");

        var link = await _links.GetByIntentAsync("intent-1", CancellationToken.None);
        link!.Snapshot.Title.Should().Be("Title");
        link.Snapshot.Description.Should().Be("New body");
    }

    [Fact(DisplayName = "IntentTextChanged: echo suppression — текст уже == TextOf(snapshot), пуша нет")]
    public async Task Text_changed_echo_suppressed()
    {
        _links.Seed(Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", "Body", "col", "Todo")));

        await Handler().HandleAsync(
            new IntentTextChanged(IntentWith("intent-1", "Body", title: "Title")), CancellationToken.None);

        _provider.Updated.Should().BeEmpty();
    }

    [Fact(DisplayName = "IntentTextChanged: интент без линка — пуша нет")]
    public async Task Text_changed_no_link()
    {
        await Handler().HandleAsync(new IntentTextChanged(IntentWith("intent-1", "Body")), CancellationToken.None);

        _provider.Updated.Should().BeEmpty();
    }

    [Fact(DisplayName = "IntentTextChanged: stub-линк не пушится")]
    public async Task Text_changed_stub_link()
    {
        var link = Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", "Old", "col", "Todo"));
        link.MarkStub(Now);
        _links.Seed(link);

        await Handler().HandleAsync(new IntentTextChanged(IntentWith("intent-1", "New")), CancellationToken.None);

        _provider.Updated.Should().BeEmpty();
    }

    [Fact(DisplayName = "IntentLinkAdded: оба линка, ребёнок ещё не привязан — вызывается AddChildAsync")]
    public async Task Link_added_calls_add_child()
    {
        _links.Seed(Linked("parent", "card-P", new CardSyncLinkSnapshot("P", null, null, null)));
        _links.Seed(Linked("child", "card-C", new CardSyncLinkSnapshot("C", null, null, null)));

        await Handler().HandleAsync(new IntentLinkAdded(Edge("parent", "child")), CancellationToken.None);

        _provider.Added.Should().ContainSingle().Which.Should().Be(("card-P", "card-C"));
    }

    [Fact(DisplayName = "IntentLinkAdded: ребёнок уже присутствует в Kaiten — echo suppression, AddChild не зовётся")]
    public async Task Link_added_echo_suppressed()
    {
        _links.Seed(Linked("parent", "card-P", new CardSyncLinkSnapshot("P", null, null, null)));
        _links.Seed(Linked("child", "card-C", new CardSyncLinkSnapshot("C", null, null, null)));
        _provider.ChildCardIds.Add("card-C");

        await Handler().HandleAsync(new IntentLinkAdded(Edge("parent", "child")), CancellationToken.None);

        _provider.Added.Should().BeEmpty();
    }

    [Fact(DisplayName = "IntentLinkRemoved: ребёнок присутствует — вызывается RemoveChildAsync")]
    public async Task Link_removed_calls_remove_child()
    {
        _links.Seed(Linked("parent", "card-P", new CardSyncLinkSnapshot("P", null, null, null)));
        _links.Seed(Linked("child", "card-C", new CardSyncLinkSnapshot("C", null, null, null)));
        _provider.ChildCardIds.Add("card-C");

        await Handler().HandleAsync(new IntentLinkRemoved(Edge("parent", "child")), CancellationToken.None);

        _provider.Removed.Should().ContainSingle().Which.Should().Be(("card-P", "card-C"));
    }

    [Fact(DisplayName = "IntentLinkRemoved: ребёнка уже нет — echo suppression, RemoveChild не зовётся")]
    public async Task Link_removed_echo_suppressed()
    {
        _links.Seed(Linked("parent", "card-P", new CardSyncLinkSnapshot("P", null, null, null)));
        _links.Seed(Linked("child", "card-C", new CardSyncLinkSnapshot("C", null, null, null)));

        await Handler().HandleAsync(new IntentLinkRemoved(Edge("parent", "child")), CancellationToken.None);

        _provider.Removed.Should().BeEmpty();
    }

    [Theory(DisplayName = "IntentStatusChanged → done/reject на stub-линке удаляет линк")]
    [InlineData(IntentStatusNames.Done)]
    [InlineData(IntentStatusNames.Reject)]
    public async Task Closing_status_on_stub_deletes_link(string status)
    {
        var link = Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", null, null, null));
        link.MarkStub(Now);
        _links.Seed(link);
        var intent = IntentWith("intent-1", "Title");
        intent.SetStatus(status, Now);

        await Handler().HandleAsync(new IntentStatusChanged(intent), CancellationToken.None);

        _links.DeletedIntentIds.Should().Contain("intent-1");
    }

    [Fact(DisplayName = "IntentStatusChanged → reject на linked-линке оставляет линк")]
    public async Task Status_reject_on_linked_keeps_link()
    {
        _links.Seed(Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", null, null, null)));
        var intent = IntentWith("intent-1", "Title");
        intent.SetStatus(IntentStatusNames.Reject, Now);

        await Handler().HandleAsync(new IntentStatusChanged(intent), CancellationToken.None);

        _links.DeletedIntentIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "Сбой провайдера проглатывается: HandleAsync не бросает")]
    public async Task Provider_failure_swallowed()
    {
        _links.Seed(Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", "Old", null, null)));
        _provider.Throw = true;

        var act = () => Handler().HandleAsync(
            new IntentTextChanged(IntentWith("intent-1", "New", title: "Title")), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static IntentLink Edge(string fromId, string toId) =>
        IntentLink.Create(Guid.NewGuid().ToString("N"), new IntentId(fromId), new IntentId(toId),
            blocking: true, IntentLinkAuthor.Agent, "kaiten:card-child", Now);
}
