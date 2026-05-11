using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Tests.Intents.Linking;

public class LinkIntentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "link_intent с from==to отдаёт link.self_link до обращения к репо")]
    public async Task Self_link_is_rejected_at_handler()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        var handler = new LinkIntentHandler(repo, new PassthroughUnitOfWork(), new FakeClock(Now));

        var same = IntentId.New().Value;
        var act = () => handler.HandleAsync(
            new LinkIntentCommand(same, same, IntentLinkType.Relates, IntentLinkAuthor.Agent, null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(LinkErrorCodes.SelfLink);
        await repo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact(DisplayName = "link_intent с duplicate_of отдаёт link.type_unsupported (stage 3 reserved)")]
    public async Task Duplicate_of_is_rejected_in_stage_one()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        var handler = new LinkIntentHandler(repo, new PassthroughUnitOfWork(), new FakeClock(Now));

        var act = () => handler.HandleAsync(
            new LinkIntentCommand(
                IntentId.New().Value,
                IntentId.New().Value,
                IntentLinkType.DuplicateOf,
                IntentLinkAuthor.Agent,
                null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(LinkErrorCodes.TypeUnsupported);
        await repo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact(DisplayName = "link_intent при duplicate в репо конвертируется в link.duplicate")]
    public async Task Repository_duplicate_is_translated_to_link_duplicate()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        var existing = IntentLink.Create(
            id: "existing",
            fromId: new IntentId("a"),
            toId: new IntentId("b"),
            type: IntentLinkType.Relates,
            author: IntentLinkAuthor.User,
            rationale: null,
            createdAt: Now);
        repo.CreateAsync(Arg.Any<IntentLink>(), Arg.Any<CancellationToken>())
            .Returns(new CreateIntentLinkOutcome.Duplicate(existing));

        var handler = new LinkIntentHandler(repo, new PassthroughUnitOfWork(), new FakeClock(Now));

        var act = () => handler.HandleAsync(
            new LinkIntentCommand("a", "b", IntentLinkType.Relates, IntentLinkAuthor.Agent, null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(LinkErrorCodes.Duplicate);
    }

    [Fact(DisplayName = "link_intent при IntentNotFound отдаёт intent.not_found с конкретным id")]
    public async Task Repository_intent_not_found_is_translated()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        repo.CreateAsync(Arg.Any<IntentLink>(), Arg.Any<CancellationToken>())
            .Returns(new CreateIntentLinkOutcome.IntentNotFound("ghost"));

        var handler = new LinkIntentHandler(repo, new PassthroughUnitOfWork(), new FakeClock(Now));

        var act = () => handler.HandleAsync(
            new LinkIntentCommand("a", "ghost", IntentLinkType.Blocks, IntentLinkAuthor.Agent, null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
        ex.Which.Extensions["intent_id"].Should().Be("ghost");
    }

    [Fact(DisplayName = "link_intent на success возвращает доменный IntentLink с переданным rationale")]
    public async Task Success_returns_link_with_rationale_trimmed()
    {
        IntentLink? captured = null;
        var repo = Substitute.For<IIntentLinkRepository>();
        repo.CreateAsync(Arg.Any<IntentLink>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<IntentLink>();
                return Task.FromResult<CreateIntentLinkOutcome>(new CreateIntentLinkOutcome.Created(captured));
            });

        var handler = new LinkIntentHandler(repo, new PassthroughUnitOfWork(), new FakeClock(Now));
        var link = await handler.HandleAsync(
            new LinkIntentCommand("a", "b", IntentLinkType.DerivedFrom, IntentLinkAuthor.Agent, "  cause  "),
            CancellationToken.None);

        link.Should().NotBeNull();
        link.Rationale.Should().Be("cause");
        link.Type.Should().Be(IntentLinkType.DerivedFrom);
        link.Author.Should().Be(IntentLinkAuthor.Agent);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
