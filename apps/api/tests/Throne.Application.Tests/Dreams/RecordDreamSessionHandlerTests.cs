using FluentAssertions;
using NSubstitute;
using Throne.Application.Dreams;
using Throne.Application.Errors;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Application.Tests.Instructions;
using Throne.Domain.Dreams;

namespace Throne.Application.Tests.Dreams;

public class RecordDreamSessionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "RecordDreamSession сохраняет сессию через unit-of-work и проставляет owner")]
    public async Task Record_persists_and_assigns_owner()
    {
        var repo = Substitute.For<IDreamSessionRepository>();
        repo.CreateAsync(Arg.Any<DreamSession>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreateDreamSessionOutcome(call.Arg<DreamSession>())));

        var handler = NewHandler(repo);
        var session = await handler.HandleAsync(
            new RecordDreamSessionCommand(
                Vendor: "claude-code",
                DateFrom: null,
                DateTo: null,
                ProcessedConversationIds: ["conv-1", "conv-2"],
                Summary: "found a thing",
                Reflection: null,
                ProposedPatchIds: []),
            CancellationToken.None);

        session.OwnerUserId.Should().Be("user-1");
        session.Payload.Vendor.Should().Be("claude-code");
        session.Identity.CreatedAt.Should().Be(Now);
        session.Payload.ProcessedConversationIds.Should().Equal("conv-1", "conv-2");
    }

    [Fact(DisplayName = "RecordDreamSession отвергает vendor, которого нет в dream_sources манифеста")]
    public async Task Record_rejects_unknown_vendor()
    {
        var handler = NewHandler(Substitute.For<IDreamSessionRepository>());

        var act = async () => await handler.HandleAsync(
            new RecordDreamSessionCommand(
                Vendor: "totally-made-up-vendor",
                DateFrom: null,
                DateTo: null,
                ProcessedConversationIds: [],
                Summary: "x",
                Reflection: null,
                ProposedPatchIds: []),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Detail.Should().Contain("Unknown vendor");
    }

    [Fact(DisplayName = "RecordDreamSession отвергает пустой summary")]
    public async Task Record_rejects_empty_summary()
    {
        var handler = NewHandler(Substitute.For<IDreamSessionRepository>());

        var act = async () => await handler.HandleAsync(
            new RecordDreamSessionCommand(
                Vendor: "claude-code",
                DateFrom: null,
                DateTo: null,
                ProcessedConversationIds: [],
                Summary: "   ",
                Reflection: null,
                ProposedPatchIds: []),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "RecordDreamSession отвергает date_from > date_to")]
    public async Task Record_rejects_inverted_date_range()
    {
        var handler = NewHandler(Substitute.For<IDreamSessionRepository>());

        var act = async () => await handler.HandleAsync(
            new RecordDreamSessionCommand(
                Vendor: "claude-code",
                DateFrom: Now.AddDays(2),
                DateTo: Now,
                ProcessedConversationIds: [],
                Summary: "ok",
                Reflection: null,
                ProposedPatchIds: []),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static RecordDreamSessionHandler NewHandler(IDreamSessionRepository repo)
    {
        var manifest = WithDreamSources();
        return new RecordDreamSessionHandler(
            repo,
            new PassthroughUnitOfWork(),
            new TestCurrentUserAccessor(),
            manifest,
            new FakeTimeProvider(Now));
    }

    private static InMemorySkillManifestProvider WithDreamSources()
    {
        var sample = SkillManifestFixtures.Sample();
        return new InMemorySkillManifestProvider(new SkillManifest(
            sample.Version,
            sample.SystemInstructions,
            sample.Bundles,
            new[] { new DreamSourceManifestEntry("claude-code", "~/.claude/projects", "hint") }));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
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
