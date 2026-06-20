using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Tests.Intents.Linking;

public class GetIntentLinksSummaryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "пустой список ids → 422 link.validation_failed")]
    public async Task Empty_ids_throws_validation()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        var handler = new GetIntentLinksSummaryHandler(repo);

        var act = () => handler.HandleAsync(new GetIntentLinksSummaryQuery([]), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "ids сверх лимита 200 → 422 link.validation_failed")]
    public async Task Over_limit_throws_validation()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        var handler = new GetIntentLinksSummaryHandler(repo);

        var ids = Enumerable.Range(0, GetIntentLinksSummaryHandler.MaxIds + 1)
            .Select(i => $"intent-{i:D4}")
            .ToList();

        var act = () => handler.HandleAsync(new GetIntentLinksSummaryQuery(ids), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "проекция раскладывает edges по blocking + direction")]
    public async Task Projects_edges_by_blocking_and_direction()
    {
        var subject = MakeIntent("subject");
        var blocker = MakeIntent("blocker");
        var blocked = MakeIntent("blocked");
        var source = MakeIntent("source");
        var target = MakeIntent("target");

        var blocksEdge = new IntentLinkView(
            new IntentLink("e1", blocker.Id, subject.Id, true, IntentLinkAuthor.User, null, Now),
            IntentLinkDirection.Incoming, blocker);
        var blocksOutgoing = new IntentLinkView(
            new IntentLink("e2", subject.Id, blocked.Id, true, IntentLinkAuthor.User, null, Now),
            IntentLinkDirection.Outgoing, blocked);
        var linkedFrom = new IntentLinkView(
            new IntentLink("e3", source.Id, subject.Id, false, IntentLinkAuthor.User, null, Now),
            IntentLinkDirection.Incoming, source);
        var linkedTo = new IntentLinkView(
            new IntentLink("e4", subject.Id, target.Id, false, IntentLinkAuthor.Agent, null, Now),
            IntentLinkDirection.Outgoing, target);

        var repo = Substitute.For<IIntentLinkRepository>();
        repo.ListByIntentsAsync(Arg.Any<IReadOnlyList<IntentId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<IntentLinkView>>(StringComparer.Ordinal)
            {
                [subject.Id.Value] = [blocksEdge, blocksOutgoing, linkedFrom, linkedTo],
            });

        var handler = new GetIntentLinksSummaryHandler(repo);
        var summaries = await handler.HandleAsync(
            new GetIntentLinksSummaryQuery([subject.Id.Value]),
            CancellationToken.None);

        var summary = summaries.Single();
        summary.IntentId.Should().Be(subject.Id.Value);
        summary.BlockedBy.Select(p => p.Id.Value).Should().ContainSingle().Which.Should().Be(blocker.Id.Value);
        summary.Blocks.Select(p => p.Id.Value).Should().ContainSingle().Which.Should().Be(blocked.Id.Value);
        summary.LinkedFrom.Select(p => p.Id.Value).Should().ContainSingle().Which.Should().Be(source.Id.Value);
        summary.LinkedTo.Select(p => p.Id.Value).Should().ContainSingle().Which.Should().Be(target.Id.Value);
    }

    [Fact(DisplayName = "блокирующая edge с direction=outgoing не попадает в blocked_by")]
    public async Task Outgoing_blocks_does_not_count_as_blocked_by()
    {
        var subject = MakeIntent("subject");
        var target = MakeIntent("target");
        var outgoing = new IntentLinkView(
            new IntentLink("e1", subject.Id, target.Id, true, IntentLinkAuthor.User, null, Now),
            IntentLinkDirection.Outgoing, target);

        var repo = Substitute.For<IIntentLinkRepository>();
        repo.ListByIntentsAsync(Arg.Any<IReadOnlyList<IntentId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<IntentLinkView>>(StringComparer.Ordinal)
            {
                [subject.Id.Value] = [outgoing],
            });

        var handler = new GetIntentLinksSummaryHandler(repo);
        var summary = (await handler.HandleAsync(
            new GetIntentLinksSummaryQuery([subject.Id.Value]),
            CancellationToken.None)).Single();

        summary.BlockedBy.Should().BeEmpty();
    }

    private static Intent MakeIntent(string id) => Intent.Restore(
        id: new IntentId(id),
        text: $"text for {id}",
        status: "draft",
        currentVersion: 1,
        tagIds: [],
        createdAt: Now,
        updatedAt: Now);
}
