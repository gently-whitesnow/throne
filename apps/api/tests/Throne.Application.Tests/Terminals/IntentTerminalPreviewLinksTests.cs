using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Terminals;
using Throne.Application.Tests.Manifest;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.PromptParts;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Application.Tests.Terminals;

public class IntentTerminalPreviewLinksTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "WorkspaceMap показывает blocking-связи без rationale и soft-связи только с rationale")]
    public async Task Workspace_map_lists_filtered_link_micro_facts()
    {
        var intentId = new IntentId("intent-main");
        var intent = Intent.Restore(intentId, "BODY", IntentStatusNames.Work, 1, [], Now, Now);
        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(intentId, Arg.Any<CancellationToken>()).Returns(intent);

        var links = Substitute.For<IIntentLinkRepository>();
        links.ListByIntentAsync(intentId, Arg.Any<CancellationToken>())
            .Returns([
                LinkView(intentId, "peer-blocking-in", blocking: true, incoming: true, rationale: null),
                LinkView(intentId, "peer-blocking-out", blocking: true, incoming: false, rationale: "дальше зависит от этого"),
                LinkView(intentId, "peer-soft-out", blocking: false, incoming: false, rationale: null),
                LinkView(intentId, "peer-soft-in", blocking: false, incoming: true, rationale: "контекст пришёл отсюда"),
            ]);

        var handler = NewHandler(intents, links);

        var preview = await handler.HandleAsync(
            new IntentTerminalPreviewQuery(intentId.Value, PromptPartModeNames.Free, null),
            CancellationToken.None);

        preview.WorkspaceMap.Should().Contain("Связи:");
        preview.WorkspaceMap.Should().Contain("- заблокирован (без причины связи)");
        preview.WorkspaceMap.Should().Contain("- блокирует: дальше зависит от этого");
        preview.WorkspaceMap.Should().Contain("- вытекает из: контекст пришёл отсюда");
        preview.WorkspaceMap.Should().NotContain("- ведёт к");
        preview.WorkspaceMap.Should().NotContain("peer-soft-in body");
    }

    private static IntentTerminalPreviewHandler NewHandler(
        IIntentRepository intents,
        IIntentLinkRepository links)
    {
        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var bindings = Substitute.For<IIntentRepositoryBindingRepository>();
        bindings.FindByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var launches = Substitute.For<IIntentTerminalLaunchStore>();
        launches.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TerminalLaunchRecord?)null);

        return new IntentTerminalPreviewHandler(
            intents,
            attachments,
            links,
            bindings,
            launches,
            NewResolver(),
            NewSkillSelection(),
            NewWorkspaceRoot(),
            NewTagNames());
    }

    private static IntentLinkView LinkView(
        IntentId intentId,
        string peerId,
        bool blocking,
        bool incoming,
        string? rationale)
    {
        var peer = Intent.Restore(new IntentId(peerId), $"{peerId} body", IntentStatusNames.Work, 1, [], Now, Now);
        var link = IntentLink.Create(
            $"link-{peerId}",
            incoming ? peer.Id : intentId,
            incoming ? intentId : peer.Id,
            blocking,
            IntentLinkAuthor.User,
            rationale,
            Now);
        return new IntentLinkView(
            link,
            incoming ? IntentLinkDirection.Incoming : IntentLinkDirection.Outgoing,
            peer);
    }

    private static IWorkspaceRootProvider NewWorkspaceRoot()
    {
        var provider = Substitute.For<IWorkspaceRootProvider>();
        provider.ResolvedRoot.Returns("/ws");
        return provider;
    }

    private static RunPreflightTagNames NewTagNames() =>
        new(Substitute.For<ITagRepository>());

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
