using FluentAssertions;
using NSubstitute;
using Throne.Application.Dreams;
using Throne.Application.Ports;
using Throne.Domain.Dreams;

namespace Throne.Application.Tests.Dreams;

public class ListDreamSessionsHandlerTests
{
    [Fact(DisplayName = "ListDreamSessions клампит limit к MaxLimit и применяет vendor-фильтр")]
    public async Task List_clamps_limit_and_passes_filter()
    {
        var repo = Substitute.For<IDreamSessionRepository>();
        DreamSessionListFilter? captured = null;
        int capturedLimit = 0;
        repo.ListAsync(
                Arg.Do<DreamSessionListFilter>(f => captured = f),
                Arg.Do<int>(l => capturedLimit = l),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new DreamSessionPage(Array.Empty<DreamSession>(), null)));

        var handler = new ListDreamSessionsHandler(repo);
        await handler.HandleAsync(
            new ListDreamSessionsQuery(Vendor: "claude-code", Host: null, Limit: 10_000, Cursor: null),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Vendor.Should().Be("claude-code");
        capturedLimit.Should().Be(ListDreamSessionsHandler.MaxLimit);
    }

    [Fact(DisplayName = "ListDreamSessions прокидывает host-фильтр и нормализует пустой host в null")]
    public async Task List_passes_and_normalises_host_filter()
    {
        var repo = Substitute.For<IDreamSessionRepository>();
        var captured = new List<DreamSessionListFilter>();
        repo.ListAsync(
                Arg.Do<DreamSessionListFilter>(f => captured.Add(f)),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new DreamSessionPage(Array.Empty<DreamSession>(), null)));

        var handler = new ListDreamSessionsHandler(repo);
        await handler.HandleAsync(
            new ListDreamSessionsQuery(Vendor: null, Host: "laptop-1.local", Limit: null, Cursor: null),
            CancellationToken.None);
        await handler.HandleAsync(
            new ListDreamSessionsQuery(Vendor: null, Host: "  ", Limit: null, Cursor: null),
            CancellationToken.None);

        captured[0].Host.Should().Be("laptop-1.local");
        captured[1].Host.Should().BeNull();
    }

    [Fact(DisplayName = "ListDreamSessions поднимает limit < 1 до 1")]
    public async Task List_clamps_limit_floor()
    {
        var repo = Substitute.For<IDreamSessionRepository>();
        int capturedLimit = 0;
        repo.ListAsync(
                Arg.Any<DreamSessionListFilter>(),
                Arg.Do<int>(l => capturedLimit = l),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new DreamSessionPage(Array.Empty<DreamSession>(), null)));

        var handler = new ListDreamSessionsHandler(repo);
        await handler.HandleAsync(
            new ListDreamSessionsQuery(Vendor: null, Host: null, Limit: 0, Cursor: null),
            CancellationToken.None);

        capturedLimit.Should().Be(1);
    }

    [Fact(DisplayName = "ListDreamSessions: пустой vendor нормализуется в null")]
    public async Task List_normalises_empty_vendor()
    {
        var repo = Substitute.For<IDreamSessionRepository>();
        DreamSessionListFilter? captured = null;
        repo.ListAsync(
                Arg.Do<DreamSessionListFilter>(f => captured = f),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new DreamSessionPage(Array.Empty<DreamSession>(), null)));

        var handler = new ListDreamSessionsHandler(repo);
        await handler.HandleAsync(
            new ListDreamSessionsQuery(Vendor: "   ", Host: null, Limit: null, Cursor: null),
            CancellationToken.None);

        captured!.Vendor.Should().BeNull();
    }
}
