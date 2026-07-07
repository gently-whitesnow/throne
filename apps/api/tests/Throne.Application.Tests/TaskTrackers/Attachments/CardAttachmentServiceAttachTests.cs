using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Attachments;
using Throne.Domain.TaskTrackers;
using static Throne.Application.Tests.TaskTrackers.Attachments.CardAttachmentServiceFixture;

namespace Throne.Application.Tests.TaskTrackers.Attachments;

/// <summary>Attach-path behaviour for <see cref="CardAttachmentService"/> (idempotency + failure codes).</summary>
public class CardAttachmentServiceAttachTests
{
    [Fact(DisplayName = "Attach happy path создаёт attachment в available с pull-снапшотом")]
    public async Task Attach_creates_available()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card(title: "Fresh"));

        var result = await fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        result.State.Availability.Should().Be(CardAvailabilityNames.Available);
        result.State.Snapshot.Title.Should().Be("Fresh");
        result.IntentId.Value.Should().Be(IntentIdValue);
        fixture.Store.Items.Should().ContainSingle();
    }

    [Fact(DisplayName = "Attach идемпотентен по координате: повторный attach обновляет тот же id")]
    public async Task Attach_is_idempotent_by_coordinate()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card(title: "First"));
        var first = await fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card(title: "Second"));
        var second = await fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        second.Id.Should().Be(first.Id);
        second.State.Snapshot.Title.Should().Be("Second");
        fixture.Store.Items.Should().ContainSingle();
    }

    [Fact(DisplayName = "Attach c отсутствующим интентом → 404 card_attachment.intent_not_found")]
    public async Task Attach_missing_intent_404()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.TrackerConnected();

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentIntentNotFound);
    }

    [Fact(DisplayName = "Attach на невалидную координату → 422 card_attachment.invalid_coordinate")]
    public async Task Attach_invalid_coordinate_422()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();

        var act = () => fixture.Service.AttachAsync(
            new AttachCardCommand(IntentIdValue, "Kaiten", BoardId, CardId), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentInvalidCoordinate);
    }

    [Fact(DisplayName = "Attach на неподдерживаемый трекер → 422 card_attachment.tracker_unsupported")]
    public async Task Attach_unsupported_tracker_422()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerUnsupported();

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentTrackerUnsupported);
    }

    [Fact(DisplayName = "Attach без коннекта → 409 card_attachment.tracker_not_connected")]
    public async Task Attach_not_connected_409()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerNotConnected();

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentTrackerNotConnected);
    }

    [Fact(DisplayName = "Attach при броске провайдера → 502 card_attachment.tracker_unavailable")]
    public async Task Attach_provider_throws_502()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ => throw new InvalidOperationException("boom");

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentTrackerUnavailable);
        fixture.Store.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Attach на пропавшую карточку (null) → 404 card_attachment.card_not_found")]
    public async Task Attach_card_gone_404()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(null);

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentCardNotFound);
        fixture.Store.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Attach на карточку с другой доски → 422 card_attachment.invalid_coordinate")]
    public async Task Attach_board_mismatch_422()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(
            Card(title: "Moved", boardId: "other-board"));

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentInvalidCoordinate);
        fixture.Store.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Attach при TaskTrackerConnectionException → 502 tracker_unavailable и фиксирует health")]
    public async Task Attach_connection_exception_records_health_and_502()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ =>
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Blocked, "tariff wall");

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentTrackerUnavailable);
        await fixture.Connections.Received().SaveHealthAsync(
            Tracker, TaskTrackerConnectionHealth.Blocked, "tariff wall",
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Attach OperationCanceled пробрасывается, не оборачивается в 502")]
    public async Task Attach_cancellation_propagates()
    {
        var fixture = new CardAttachmentServiceFixture();
        fixture.IntentExists();
        fixture.TrackerConnected();
        fixture.Provider.OnGetCard = _ => throw new OperationCanceledException();

        var act = () => fixture.Service.AttachAsync(AttachCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
