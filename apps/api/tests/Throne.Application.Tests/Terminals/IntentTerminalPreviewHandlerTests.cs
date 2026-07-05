using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
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

namespace Throne.Application.Tests.Terminals;

public class IntentTerminalPreviewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Без аттачей UserPrompt равен Intent.text без блока attachments")]
    public async Task No_attachments_keeps_user_prompt_as_intent_text()
    {
        var handler = NewHandler(
            intentText: "пишем код",
            attachments: [],
            out var intentId);

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Free, null),
            CancellationToken.None);

        preview.Composition.UserPrompt.Should().Be("пишем код");
        preview.WorkspaceMap.Should().NotContain("Приложения интента");
    }

    [Fact(DisplayName = "Аттач идёт в карту workspace, а тело задачи остаётся чистым Intent.text")]
    public async Task Attachment_goes_to_workspace_map_not_user_prompt()
    {
        var attachment = new IntentAttachment("att-1", "intent-1", "shot.png", "image/png", 12345, Now);
        var handler = NewHandler(
            intentText: "посмотри картинку",
            attachments: [attachment],
            out var intentId);

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Free, null),
            CancellationToken.None);

        // Тело — только текст интента; приложения больше не подклеиваются к редактируемой зоне.
        preview.Composition.UserPrompt.Should().Be("посмотри картинку");
        preview.WorkspaceMap.Should().Contain("Приложения интента (открой через Read):");
        preview.WorkspaceMap.Should().Contain("- \"shot.png\": .throne/attachments/att-1-shot.png");
    }

    [Fact(DisplayName = "Отсутствующий интент даёт IntentNotFound и не дёргает репозиторий аттачей")]
    public async Task Missing_intent_throws_and_skips_attachments_lookup()
    {
        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns((Intent?)null);
        var attachments = Substitute.For<IIntentAttachmentRepository>();
        var handler = new IntentTerminalPreviewHandler(
            intents,
            attachments,
            NewBindings([]),
            NewLaunches(),
            NewResolver(),
            NewSkillSelection(),
            NewWorkspaceMap(NewLinks([])));

        var act = () => handler.HandleAsync(
            new IntentTerminalPreviewQuery("missing-id", PromptPartModeNames.Free, null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
        await attachments.DidNotReceiveWithAnyArgs().ListByIntentAsync(default!, default);
    }

    [Fact(DisplayName = "Preview отдаёт доступные скилы с причиной, если пакет нельзя материализовать")]
    public async Task Preview_returns_available_skills_with_materialization_reason()
    {
        var handler = NewHandler(
            intentText: "пишем код",
            attachments: [],
            out var intentId);

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Review, null),
            CancellationToken.None);

        preview.AvailableSkills.Should().Contain(s =>
            s.SkillId == SessionSkillPackageIds.Intent
            && s.Materializable
            && !s.Selected);
        preview.AvailableSkills.Should().Contain(s =>
            s.SkillId == SessionSkillPackageIds.Review
            && !s.Materializable
            && s.Reason == ReviewArtifactWriteTarget.NoBindingReason
            && !s.Selected);
    }

    [Fact(DisplayName = "WorkspaceMap несёт корень, все repo-пути (без статуса клона) и теги")]
    public async Task Workspace_map_lists_root_repos_and_tags()
    {
        var intentId = IntentId.New();
        var tag = Tag.Create(TagId.New(), "throne", Now);
        var intent = Intent.Restore(
            intentId, "тело", IntentStatusNames.Work, 1, [tag.Id], Now, Now, title: "Протянуть title");

        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns(intent);
        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        attachmentRepo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var bindings = new[]
        {
            Binding("/ws/intents/repo-ready", CloneStatusNames.Ready),
            Binding("/ws/intents/repo-cloning", CloneStatusNames.Cloning),
        };

        var handler = new IntentTerminalPreviewHandler(
            intents,
            attachmentRepo,
            NewBindings(bindings),
            NewLaunches(),
            NewResolver(),
            NewSkillSelection(),
            NewWorkspaceMap(NewLinks([]), "/ws", tag));

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Free, null),
            CancellationToken.None);

        preview.WorkspaceMap.Should().Contain($"Корень workspace: /ws/intents/{intentId.Value}");
        preview.WorkspaceMap.Should().Contain("- /ws/intents/repo-ready\n");
        // Неготовый клон тоже в списке — без пометки статуса: к спавну он будет склонирован.
        preview.WorkspaceMap.Should().Contain("- /ws/intents/repo-cloning");
        preview.WorkspaceMap.Should().NotContain("клон:");
        preview.WorkspaceMap.Should().Contain("Заголовок интента: Протянуть title");
        preview.WorkspaceMap.Should().Contain("Теги интента: throne");
        // Карта самодостаточна — тело сюда не вкладываем (иначе на доставке оно дублируется).
        preview.WorkspaceMap.Should().NotContain("тело");
    }

    [Fact(DisplayName = "WorkspaceMap несёт строку выбранных скиллов сессии")]
    public async Task Workspace_map_lists_selected_session_skills()
    {
        var intentId = IntentId.New();
        var intent = Intent.Restore(intentId, "тело", IntentStatusNames.Work, 1, [], Now, Now);

        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns(intent);
        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        attachmentRepo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        // Review mode default-enables the review skill; a ready binding makes it materialisable → selected.
        var bindings = new[] { Binding("/ws/intents/repo-ready", CloneStatusNames.Ready) };

        var handler = new IntentTerminalPreviewHandler(
            intents,
            attachmentRepo,
            NewBindings(bindings),
            NewLaunches(),
            NewResolver(),
            NewSkillSelection(),
            NewWorkspaceMap(NewLinks([])));

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Review, null),
            CancellationToken.None);

        preview.WorkspaceMap.Should().Contain($"Скиллы сессии: {SessionSkillPackageIds.Review}");
    }

    private static IntentRepositoryBinding Binding(string workspacePath, string cloneStatus) =>
        IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
            Id: new BindingId(workspacePath),
            IntentId: new IntentId("intent-1"),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "repo"),
            WorkspacePath: workspacePath,
            DefaultBranch: "main",
            CloneStatus: cloneStatus,
            CloneError: null,
            PullRequestNumber: null,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now));

    private static IntentTerminalPreviewHandler NewHandler(
        string intentText,
        IReadOnlyList<IntentAttachment> attachments,
        out IntentId intentId)
    {
        intentId = IntentId.New();
        var intent = Intent.Restore(intentId, intentText, IntentStatusNames.Work, 1, [], Now, Now);

        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(intent);

        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        attachmentRepo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(attachments);

        return new IntentTerminalPreviewHandler(
            intents,
            attachmentRepo,
            NewBindings([]),
            NewLaunches(),
            NewResolver(),
            NewSkillSelection(),
            NewWorkspaceMap(NewLinks([])));
    }

    private static IWorkspaceRootProvider NewWorkspaceRoot(string root = "/ws")
    {
        var provider = Substitute.For<IWorkspaceRootProvider>();
        provider.ResolvedRoot.Returns(root);
        return provider;
    }

    private static RunPreflightTagNames NewTagNames(params Tag[] tags)
    {
        var repo = Substitute.For<ITagRepository>();
        foreach (var tag in tags)
        {
            repo.GetByIdAsync(tag.Id, Arg.Any<CancellationToken>()).Returns(tag);
        }
        return new RunPreflightTagNames(repo);
    }

    private static IIntentTerminalLaunchStore NewLaunches()
    {
        var store = Substitute.For<IIntentTerminalLaunchStore>();
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TerminalLaunchRecord?)null);
        return store;
    }

    private static IIntentRepositoryBindingRepository NewBindings(IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        var repo = Substitute.For<IIntentRepositoryBindingRepository>();
        repo.FindByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(bindings);
        return repo;
    }

    private static IIntentLinkRepository NewLinks(IReadOnlyList<IntentLinkView> links)
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        repo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(links);
        return repo;
    }

    private static IntentWorkspaceMapComposer NewWorkspaceMap(
        IIntentLinkRepository links,
        string root = "/ws",
        params Tag[] tags) =>
        new(NewWorkspaceRoot(root), NewTagNames(tags), new IntentLinkPromptContextReader(links));

    private static PromptCompositionResolver NewResolver()
    {
        var repo = Substitute.For<IPromptPartRepository>();
        repo.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PromptPart>());
        return new PromptCompositionResolver(
            SkillManifestFixtures.Provider(),
            repo);
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
