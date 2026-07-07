using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Terminals;
using Throne.Application.Tests.Manifest;
using Throne.Domain.Intents;
using Throne.Domain.PromptParts;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.Terminals;

public class IntentTerminalPreviewCardAttachmentsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Preview показывает снапшоты карточек в карте, не в user prompt")]
    public async Task Preview_renders_card_snapshots_in_workspace_map()
    {
        var intentId = IntentId.New();
        var (handler, _) = NewHandler(intentId, [CardAttachment(intentId, archived: true)]);

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Free, null),
            CancellationToken.None);

        preview.Composition.UserPrompt.Should().Be("исходная задача");
        preview.WorkspaceMap.Should().Contain("Приложенные карточки интента:");
        preview.WorkspaceMap.Should().Contain("[card linear/board-7/card-42] (в архиве)");
        preview.WorkspaceMap.Should().Contain("Title: Fix preview");
        preview.WorkspaceMap.Should().Contain("ColumnTitle: Review");
        preview.WorkspaceMap.Should().Contain("Description:\n## Context\nUse snapshot.");
        preview.WorkspaceMap.Should().NotContain("исходная задача");
    }

    [Fact(DisplayName = "Preview без карточек не добавляет карточный блок")]
    public async Task Preview_without_cards_omits_card_block()
    {
        var intentId = IntentId.New();
        var (handler, _) = NewHandler(intentId, []);

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Free, null),
            CancellationToken.None);

        preview.WorkspaceMap.Should().NotContain("Приложенные карточки интента");
    }

    private static (IntentTerminalPreviewHandler Handler, IntentId IntentId) NewHandler(
        IntentId intentId,
        IReadOnlyList<IntentCardAttachment> cardAttachments)
    {
        var intent = Intent.Restore(intentId, "исходная задача", IntentStatusNames.Work, 1, [], Now, Now);
        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(intentId, Arg.Any<CancellationToken>()).Returns(intent);

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.ListByIntentAsync(intentId, Arg.Any<CancellationToken>()).Returns([]);
        var cardStore = Substitute.For<IIntentCardAttachmentStore>();
        cardStore.ListByIntentAsync(intentId, Arg.Any<CancellationToken>()).Returns(cardAttachments);
        var bindings = Substitute.For<IIntentRepositoryBindingRepository>();
        bindings.FindByIntentAsync(intentId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<IntentRepositoryBinding>());

        return (new IntentTerminalPreviewHandler(
            intents,
            attachments,
            cardStore,
            bindings,
            EmptyLaunches(),
            NewResolver(),
            NewSkillSelection(),
            NewWorkspaceMap()),
            intentId);
    }

    private static IntentCardAttachment CardAttachment(IntentId intentId, bool archived) =>
        IntentCardAttachment.Create(
            CardAttachmentId.New(),
            intentId,
            new CardCoordinate("linear", "board-7", "card-42"),
            new CardSnapshot(
                "Fix preview", "## Context\nUse snapshot.", "Review", archived, "v1", Now),
            Now);

    private static IIntentTerminalLaunchStore EmptyLaunches()
    {
        var launches = Substitute.For<IIntentTerminalLaunchStore>();
        launches.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TerminalLaunchRecord?)null);
        return launches;
    }

    private static IntentWorkspaceMapComposer NewWorkspaceMap()
    {
        var root = Substitute.For<IWorkspaceRootProvider>();
        root.ResolvedRoot.Returns("/ws");
        var tags = new RunPreflightTagNames(Substitute.For<ITagRepository>());
        var links = new IntentLinkPromptContextReader(Substitute.For<IIntentLinkRepository>());
        return new IntentWorkspaceMapComposer(root, tags, links);
    }

    private static PromptCompositionResolver NewResolver()
    {
        var repo = Substitute.For<IPromptPartRepository>();
        repo.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PromptPart>());
        return new PromptCompositionResolver(SkillManifestFixtures.Provider(), repo);
    }

    private static SessionSkillSelectionService NewSkillSelection()
    {
        var catalog = TerminalSpawnTestDoubles.SkillCatalog();
        var defaults = Substitute.For<ISkillModeDefaultStore>();
        defaults.ListAsync(Arg.Any<CancellationToken>())
            .Returns(SkillModeDefaultSeeds.Build(catalog));
        return new SessionSkillSelectionService(catalog, defaults);
    }
}
