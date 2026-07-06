using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Attachments;
using Throne.Domain.TaskTrackers;
using static Throne.Application.Tests.TaskTrackers.Attachments.CardAttachmentServiceFixture;

namespace Throne.Application.Tests.TaskTrackers.Attachments;

/// <summary>Refresh (degrade-without-error) and detach (idempotent) behaviour for <see cref="CardAttachmentService"/>.</summary>
public class CardAttachmentServiceRefreshTests
{
    [Fact(DisplayName = "Refresh success обновляет снапшот и ставит available")]
    public async Task Refresh_success_available()
    {
        var fixture = new CardAttachmentServiceFixture();
        var attachment = await fixture.SeedAttachedAsync();

        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card(title: "Refreshed"));
        var result = await fixture.Service.RefreshAsync(
            new RefreshCardAttachmentCommand(IntentIdValue, attachment.Id.Value), CancellationToken.None);

        result.State.Availability.Should().Be(CardAvailabilityNames.Available);
        result.State.Snapshot.Title.Should().Be("Refreshed");
    }

    [Fact(DisplayName = "Refresh без коннекта деградирует в unavailable, снапшот сохраняется")]
    public async Task Refresh_not_connected_degrades_unavailable()
    {
        var fixture = new CardAttachmentServiceFixture();
        var attachment = await fixture.SeedAttachedAsync();
        fixture.TrackerNotConnected();

        var result = await fixture.Service.RefreshAsync(
            new RefreshCardAttachmentCommand(IntentIdValue, attachment.Id.Value), CancellationToken.None);

        result.State.Availability.Should().Be(CardAvailabilityNames.Unavailable);
        result.State.Snapshot.Title.Should().Be("Seed");
    }

    [Fact(DisplayName = "Refresh при броске провайдера деградирует в unavailable")]
    public async Task Refresh_provider_throws_degrades_unavailable()
    {
        var fixture = new CardAttachmentServiceFixture();
        var attachment = await fixture.SeedAttachedAsync();
        fixture.Provider.OnGetCard = _ => throw new InvalidOperationException("boom");

        var result = await fixture.Service.RefreshAsync(
            new RefreshCardAttachmentCommand(IntentIdValue, attachment.Id.Value), CancellationToken.None);

        result.State.Availability.Should().Be(CardAvailabilityNames.Unavailable);
        result.State.Snapshot.Title.Should().Be("Seed");
    }

    [Fact(DisplayName = "Refresh на пропавшую карточку (null) ставит gone, снапшот сохраняется")]
    public async Task Refresh_card_gone_marks_gone()
    {
        var fixture = new CardAttachmentServiceFixture();
        var attachment = await fixture.SeedAttachedAsync();
        fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(null);

        var result = await fixture.Service.RefreshAsync(
            new RefreshCardAttachmentCommand(IntentIdValue, attachment.Id.Value), CancellationToken.None);

        result.State.Availability.Should().Be(CardAvailabilityNames.Gone);
        result.State.Snapshot.Title.Should().Be("Seed");
    }

    [Fact(DisplayName = "Refresh на несуществующий attachment → 404 card_attachment.not_found")]
    public async Task Refresh_missing_attachment_404()
    {
        var fixture = new CardAttachmentServiceFixture();

        var act = () => fixture.Service.RefreshAsync(
            new RefreshCardAttachmentCommand(IntentIdValue, "nope"), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code
            .Should().Be(ErrorCodes.CardAttachmentNotFound);
    }

    [Fact(DisplayName = "Detach удаляет существующий attachment")]
    public async Task Detach_removes_existing()
    {
        var fixture = new CardAttachmentServiceFixture();
        var attachment = await fixture.SeedAttachedAsync();

        await fixture.Service.DetachAsync(
            new DetachCardCommand(IntentIdValue, attachment.Id.Value), CancellationToken.None);

        fixture.Store.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Detach идемпотентен: отсутствующий/чужой attachment → no-op без ошибки")]
    public async Task Detach_missing_is_noop()
    {
        var fixture = new CardAttachmentServiceFixture();
        var attachment = await fixture.SeedAttachedAsync();

        await fixture.Service.DetachAsync(
            new DetachCardCommand("other-intent", attachment.Id.Value), CancellationToken.None);
        await fixture.Service.DetachAsync(
            new DetachCardCommand(IntentIdValue, "nope"), CancellationToken.None);

        fixture.Store.Items.Should().ContainSingle();
    }
}
