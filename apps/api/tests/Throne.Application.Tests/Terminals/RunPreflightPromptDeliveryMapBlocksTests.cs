using FluentAssertions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.Terminals;

public partial class RunPreflightPromptDeliveryTests
{
    [Fact(DisplayName = "Приложения интента и скиллы сессии попадают в карту, но не в тело задачи")]
    public async Task Attachments_and_skills_render_in_map_not_in_task_body()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        var events = Substitute.For<IDomainEventDispatcher>();
        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        var shot = new IntentAttachment("att-1", IntentId, "shot.png", "image/png", 10, DateTimeOffset.UnixEpoch);
        attachmentRepo.ListByIntentAsync(new IntentId(IntentId), Arg.Any<CancellationToken>())
            .Returns([shot]);
        var (sut, workspace) = NewDelivery(tmux, events, attachmentRepo: attachmentRepo);

        try
        {
            await sut.DeliverAsync(
                NewRequest(
                    workspace, adapter: null, userPrompt: "do the thing",
                    sessionSkillIds: ["intent", "review"]),
                CancellationToken.None);

            var delivered = await File.ReadAllTextAsync(
                Path.Combine(workspace, "throne-session.user-prompt.txt"));
            delivered.Should().Contain("Скиллы сессии: intent, review");
            delivered.Should().Contain("Приложения интента (открой через Read):");
            delivered.Should().Contain("- \"shot.png\": .throne/attachments/att-1-shot.png");
            delivered.IndexOf("Приложения интента", StringComparison.Ordinal)
                .Should().BeLessThan(delivered.IndexOf("do the thing", StringComparison.Ordinal));
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    [Fact(DisplayName = "Снапшоты приложенных карточек попадают в карту delivery над задачей")]
    public async Task Card_snapshots_render_in_delivered_workspace_map()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        var events = Substitute.For<IDomainEventDispatcher>();
        var cardStore = Substitute.For<IIntentCardAttachmentStore>();
        cardStore.ListByIntentAsync(new IntentId(IntentId), Arg.Any<CancellationToken>())
            .Returns([CardAttachment(archived: true)]);
        var (sut, workspace) = NewDelivery(tmux, events, cardAttachmentStore: cardStore);

        try
        {
            await sut.DeliverAsync(
                NewRequest(workspace, adapter: null, userPrompt: "do the thing"),
                CancellationToken.None);

            var delivered = await File.ReadAllTextAsync(
                Path.Combine(workspace, "throne-session.user-prompt.txt"));
            delivered.Should().Contain("Приложенные карточки интента:");
            delivered.Should().Contain("[card linear/board-7/card-42] (в архиве)");
            delivered.Should().Contain("Title: Fix preview");
            delivered.Should().Contain("ColumnTitle: Review");
            delivered.Should().Contain("Description:\n## Context\nUse snapshot.");
            delivered.IndexOf("Приложенные карточки", StringComparison.Ordinal)
                .Should().BeLessThan(delivered.IndexOf("do the thing", StringComparison.Ordinal));
            delivered[delivered.IndexOf("do the thing", StringComparison.Ordinal)..]
                .Should().NotContain("Fix preview");
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    private static IntentCardAttachment CardAttachment(bool archived) =>
        IntentCardAttachment.Create(
            CardAttachmentId.New(),
            new IntentId(IntentId),
            new CardCoordinate("linear", "board-7", "card-42"),
            new CardSnapshot(
                "Fix preview", "## Context\nUse snapshot.", "Review", archived, "v1", DateTimeOffset.UnixEpoch),
            DateTimeOffset.UnixEpoch);
}
