using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers.Sync;

/// <summary>
/// Title write-through: <c>IntentTitleChanged</c> pushes only the card title, independently of the
/// description, so a title rename never clobbers <c>card.description</c>. Kept in its own class so the
/// per-type public-member budget stays satisfied.
/// </summary>
public class TaskTrackerWriteThroughTitleTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeCardLinkStore _links = new();
    private readonly FakeConnectionStore _connections = new();
    private readonly RecordingSyncProvider _provider = new();

    public TaskTrackerWriteThroughTitleTests() =>
        _connections.Seed("kaiten", new TaskTrackerStoredConnection("https://acme.kaiten.ru", "tok", []));

    private TaskTrackerWriteThroughHandler Handler() =>
        new(_links, _connections, [_provider], new FakeTimeProvider(Now),
            NullLogger<TaskTrackerWriteThroughHandler>.Instance);

    private static CardSyncLink Linked(string intentId, string cardId, CardSyncLinkSnapshot snapshot) =>
        CardSyncLink.Create(intentId, new TaskTrackerCardLink("kaiten", "board-7", cardId), snapshot, CardSyncLinkCursors.Empty, Now);

    private static Intent IntentWith(string id, string text, string? title) =>
        Intent.Create(new IntentId(id), text, null, Now, title: title);

    [Fact(DisplayName = "IntentTitleChanged: расхождение со снапшотом пушит только title и двигает его в снапшоте")]
    public async Task Title_changed_pushes_title_only()
    {
        _links.Seed(Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Old title", "Body", "col", "Todo")));

        await Handler().HandleAsync(
            new IntentTitleChanged(IntentWith("intent-1", "Body", title: "New title")), CancellationToken.None);

        _provider.Updated.Should().ContainSingle();
        var (cardId, patch) = _provider.Updated[0];
        cardId.Should().Be("card-42");
        patch.Title.Should().Be("New title");
        patch.Description.Should().BeNull();

        var link = await _links.GetByIntentAsync("intent-1", CancellationToken.None);
        link!.Snapshot.Title.Should().Be("New title");
        link.Snapshot.Description.Should().Be("Body");
    }

    [Fact(DisplayName = "IntentTitleChanged: title совпадает со снапшотом — echo suppression, пуша нет")]
    public async Task Title_changed_echo_suppressed()
    {
        _links.Seed(Linked("intent-1", "card-42", new CardSyncLinkSnapshot("Title", "Body", "col", "Todo")));

        await Handler().HandleAsync(
            new IntentTitleChanged(IntentWith("intent-1", "Body", title: "Title")), CancellationToken.None);

        _provider.Updated.Should().BeEmpty();
    }
}
