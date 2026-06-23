using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Xunit;

namespace Throne.Application.Tests.Intents;

/// <summary>
/// Seam between the live tmux session set and the `terminal_running` surfaces: the list
/// filter restricts to the running intent ids, and the context counter forwards them to
/// the repository. tmux is the single source of truth, so the handlers must read it fresh.
/// </summary>
public sealed class TerminalRunningContextTests
{
    private static readonly string[] TwoIds = ["aaa", "bbb"];
    private static readonly string[] OneId = ["aaa"];

    [Theory(DisplayName = "TryIntentId снимает префикс throne- и отбраковывает чужие/пустые имена")]
    [InlineData("throne-abc", "abc")]
    [InlineData("throne-", null)]
    [InlineData("other-abc", null)]
    [InlineData("", null)]
    // The shared opencode-serve session is infrastructure, not an intent — must not be surfaced.
    [InlineData("throne-opencode-serve", null)]
    public void TryIntentId_strips_prefix(string sessionName, string? expected)
    {
        TmuxSessionName.TryIntentId(sessionName).Should().Be(expected);
    }

    [Fact(DisplayName = "list terminal_running=true передаёт в spec id'шники живых tmux-сессий")]
    public async Task List_terminal_running_passes_live_ids_to_spec()
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.ListPagedAsync(Arg.Any<IntentListSpec>(), Arg.Any<CancellationToken>())
            .Returns(new IntentListPage([], NextCursor: null));
        var tags = Substitute.For<ITagRepository>();
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.ListThroneSessionsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(["throne-aaa", "throne-bbb", "noise"]);

        var handler = new ListIntentsHandler(repo, tags, tmux);
        await handler.HandleAsync(
            new ListIntentsPagedQuery(TerminalRunning: true), CancellationToken.None);

        await repo.Received(1).ListPagedAsync(
            Arg.Is<IntentListSpec>(s => s.Ids != null && s.Ids.SequenceEqual(TwoIds)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "list terminal_running=true без живых сессий отдаёт пустую страницу без запроса в БД")]
    public async Task List_terminal_running_short_circuits_empty()
    {
        var repo = Substitute.For<IIntentRepository>();
        var tags = Substitute.For<ITagRepository>();
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.ListThroneSessionsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>([]);

        var handler = new ListIntentsHandler(repo, tags, tmux);
        var page = await handler.HandleAsync(
            new ListIntentsPagedQuery(TerminalRunning: true), CancellationToken.None);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
        await repo.DidNotReceive().ListPagedAsync(Arg.Any<IntentListSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "contexts forwards живые tmux id'шники в repository для счётчика terminal_running")]
    public async Task Contexts_forwards_running_ids()
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.GetContextCountsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new IntentContextCounts(0, 0, 0, 0, 0, 0, 0, [], [], [], TerminalRunning: 1));
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.ListThroneSessionsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(["throne-aaa"]);

        var handler = new GetIntentContextsHandler(repo, tmux);
        var counts = await handler.HandleAsync(CancellationToken.None);

        counts.TerminalRunning.Should().Be(1);
        await repo.Received(1).GetContextCountsAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(OneId)),
            Arg.Any<CancellationToken>());
    }
}
