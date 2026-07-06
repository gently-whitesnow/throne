using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;
using Throne.Infrastructure.EfCore.Rows;
using static Throne.Infrastructure.Tests.EfCore.Persistence.IntentCardAttachments.IntentCardAttachmentTestFactory;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.IntentCardAttachments;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCoreIntentCardAttachmentStoreTests(SqliteFixture fixture)
{
    [Fact(DisplayName = "UpsertAsync вставляет новую запись в intent_card_attachments")]
    public async Task Upsert_inserts_row()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var attachment = NewAttachment(intentId, cardId: "77");

        await UpsertAsync(scope, attachment);

        var row = await FindRowAsync(scope.Database, attachment.Id.Value);
        row.Should().NotBeNull();
        row!.IntentId.Should().Be(intentId.Value);
        row.Tracker.Should().Be("kaiten");
        row.CardId.Should().Be("77");
        row.Availability.Should().Be(CardAvailabilityNames.Available);
    }

    [Fact(DisplayName = "UpsertAsync по существующему id обновляет снапшот и availability (без второй строки)")]
    public async Task Upsert_updates_existing()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var attachment = NewAttachment(intentId);
        await UpsertAsync(scope, attachment);

        attachment.MarkUnavailable(CardAvailabilityNames.Gone, Now.AddMinutes(1));
        await UpsertAsync(scope, attachment);

        var stored = await scope.Store.GetAsync(attachment.Id, CancellationToken.None);
        stored!.State.Availability.Should().Be(CardAvailabilityNames.Gone);
        var all = await scope.Store.ListByIntentAsync(intentId, CancellationToken.None);
        all.Should().ContainSingle();
    }

    [Fact(DisplayName = "UpsertAsync при конфликте unique-координаты обновляет выигравшую строку")]
    public async Task Upsert_unique_coordinate_conflict_updates_existing()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var first = NewAttachment(intentId, cardId: "99", title: "First");
        var racing = NewAttachment(intentId, cardId: "99", title: "Second", at: Now.AddMinutes(1));
        await UpsertAsync(scope, first);

        var persisted = await UpsertAsync(scope, racing);

        persisted.Id.Should().Be(first.Id);
        persisted.State.Snapshot.Title.Should().Be("Second");
        var all = await scope.Store.ListByIntentAsync(intentId, CancellationToken.None);
        all.Should().ContainSingle();
        all[0].Id.Should().Be(first.Id);
        all[0].State.Snapshot.Title.Should().Be("Second");
    }

    [Fact(DisplayName = "GetByCoordinateAsync находит запись по (intent, tracker, board, card)")]
    public async Task GetByCoordinate_returns_match()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var attachment = NewAttachment(intentId, cardId: "88");
        await UpsertAsync(scope, attachment);

        var found = await scope.Store.GetByCoordinateAsync(
            intentId, new CardCoordinate("kaiten", "10", "88"), CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(attachment.Id);
    }

    [Fact(DisplayName = "GetByCoordinateAsync возвращает null для другой координаты")]
    public async Task GetByCoordinate_returns_null_when_absent()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        await UpsertAsync(scope, NewAttachment(intentId, cardId: "1"));

        var found = await scope.Store.GetByCoordinateAsync(
            intentId, new CardCoordinate("kaiten", "10", "2"), CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact(DisplayName = "ListByIntentAsync фильтрует по intent и сортирует по created_at")]
    public async Task List_by_intent_scopes_and_orders()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var mine = IntentId.New();
        var other = IntentId.New();
        await UpsertAsync(scope, NewAttachment(mine, cardId: "a", at: Now));
        await UpsertAsync(scope, NewAttachment(mine, cardId: "b", at: Now.AddMinutes(1)));
        await UpsertAsync(scope, NewAttachment(other, cardId: "c"));

        var list = await scope.Store.ListByIntentAsync(mine, CancellationToken.None);

        list.Select(a => a.Coordinate.CardId).Should().ContainInOrder("a", "b");
        list.Should().HaveCount(2);
    }

    [Fact(DisplayName = "DeleteAsync удаляет запись и возвращает true; повторно — false")]
    public async Task Delete_removes_and_is_idempotent()
    {
        var scope = await IntentCardAttachmentTestScope.CreateAsync(fixture);
        var attachment = NewAttachment(IntentId.New());
        await UpsertAsync(scope, attachment);

        var first = await DeleteAsync(scope, attachment.Id);
        var second = await DeleteAsync(scope, attachment.Id);

        first.Should().BeTrue();
        second.Should().BeFalse();
        (await scope.Store.GetAsync(attachment.Id, CancellationToken.None)).Should().BeNull();
    }

    private static Task<IntentCardAttachment> UpsertAsync(IntentCardAttachmentTestScope scope, IntentCardAttachment attachment) =>
        scope.UnitOfWork.ExecuteAsync(ct => scope.Store.UpsertAsync(attachment, ct), CancellationToken.None);

    private static Task<bool> DeleteAsync(IntentCardAttachmentTestScope scope, CardAttachmentId id) =>
        scope.UnitOfWork.ExecuteAsync(ct => scope.Store.DeleteAsync(id, ct), CancellationToken.None);

    private static async Task<IntentCardAttachmentRow?> FindRowAsync(SqliteTestDatabase database, string id)
    {
        await using var ctx = await database.CreateContextAsync();
        return await ctx.Set<IntentCardAttachmentRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }
}
