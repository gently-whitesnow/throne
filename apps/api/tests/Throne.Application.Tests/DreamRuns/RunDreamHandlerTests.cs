using FluentAssertions;
using NSubstitute;
using Throne.Application.DreamRuns;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.DreamRuns;

public class RunDreamHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Пустое окно возвращает not_enough_context без создания run")]
    public async Task Empty_window_returns_not_enough_context()
    {
        var fixture = new Fixture();
        fixture.Window.CollectIntentsAsync(default).ReturnsForAnyArgs([]);

        var result = await fixture.Handler.HandleAsync(new RunDreamCommand(null), CancellationToken.None);

        result.Status.Should().Be(RunDreamResultStatuses.NotEnoughContext);
        result.DreamRun.Should().BeNull();
        result.Reason.Should().NotBeNull();
        await fixture.Runs.DidNotReceive().CreateAsync(Arg.Any<DreamRun>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Идемпотентность: pending run < 24h возвращает existing_pending")]
    public async Task Existing_pending_within_24h_is_returned()
    {
        var fixture = new Fixture();
        var existing = SampleRun(createdAt: Now.AddHours(-2));
        fixture.Runs.ListPendingAsync(default).ReturnsForAnyArgs([existing]);
        fixture.Window.CollectIntentsAsync(default).ReturnsForAnyArgs(
            new[] { Intent("intent-1", "current text", []) });

        var result = await fixture.Handler.HandleAsync(new RunDreamCommand(null), CancellationToken.None);

        result.Status.Should().Be(RunDreamResultStatuses.ExistingPending);
        result.DreamRun!.Run.Id.Should().Be(existing.Id);
        await fixture.Runs.DidNotReceive().CreateAsync(Arg.Any<DreamRun>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Есть intents с активностью → создаёт run с intent_refs и evidence_summary")]
    public async Task Has_content_creates_run_with_summary()
    {
        var fixture = new Fixture();
        fixture.Window.CollectIntentsAsync(default).ReturnsForAnyArgs(new[]
        {
            Intent("intent-1", "intent text one", new[]
            {
                new IntentQaSnapshot("qa-1", "Q1", "A1", Now.AddHours(-2)),
            }),
            Intent("intent-2", "intent text two", new[]
            {
                new IntentQaSnapshot("qa-2", "Q2", "A2", Now.AddHours(-3)),
            }),
        });
        fixture.Instructions
            .GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([SeededWorkInstruction()]);

        var result = await fixture.Handler.HandleAsync(new RunDreamCommand("auto"), CancellationToken.None);

        result.Status.Should().Be(RunDreamResultStatuses.Created);
        result.DreamRun.Should().NotBeNull();
        result.DreamRun!.IntentRefs.Should().HaveCount(2);
        result.DreamRun.EvidenceSummary.IntentCount.Should().Be(2);
        result.DreamRun.EvidenceSummary.TokenCount.Should().BeGreaterThan(0);
        result.DreamRun.EvidenceSummary.ExistingLearnedRulesByKind[InstructionKindNames.Work]
            .Single().RuleText.Should().Be("Already learned thing");
        result.DreamRun.EvidenceSummary.ExistingLearnedRulesByKind[InstructionKindNames.Common].Should().BeEmpty();
        await fixture.Runs.Received(1).CreateAsync(Arg.Any<DreamRun>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Неизвестная policy → ApiException(validation.failed)")]
    public async Task Unknown_policy_throws_validation()
    {
        var fixture = new Fixture();

        var act = () => fixture.Handler.HandleAsync(new RunDreamCommand("rich"), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static IntentInWindow Intent(string id, string text, IReadOnlyList<IntentQaSnapshot> qa) =>
        new(id, text, [], qa, [], Now.AddHours(-1));

    private static DreamRun SampleRun(DateTimeOffset createdAt) => DreamRun.Create(
        DreamRunId.New(),
        ownerUserId: "user-1",
        tokenCount: 100,
        [IntentRef.Create("intent-existing", 100, createdAt)],
        createdAt);

    private static Instruction SeededWorkInstruction() => Instruction.Restore(
        new InstructionId("inst-work"),
        InstructionScopeNames.User,
        userId: "user-1",
        kind: InstructionKindNames.Work,
        text: "# Work\n\n## Learned rules\n\n- Already learned thing\n",
        currentVersion: 3,
        createdAt: Now.AddDays(-30),
        updatedAt: Now.AddDays(-1));

    private sealed class Fixture
    {
        public IDreamRunRepository Runs { get; } = Substitute.For<IDreamRunRepository>();
        public IIntentWindowQueries Window { get; } = Substitute.For<IIntentWindowQueries>();
        public IInstructionRepository Instructions { get; } = Substitute.For<IInstructionRepository>();
        public RunDreamHandler Handler { get; }

        public Fixture()
        {
            var clock = new FakeTimeProvider(Now);
            Runs.ListPendingAsync(default).ReturnsForAnyArgs(Array.Empty<DreamRun>());
            Runs.GetProcessedIntentIdsAsync(default).ReturnsForAnyArgs(Array.Empty<string>());
            Runs.GetLockedIntentIdsAsync(default).ReturnsForAnyArgs(Array.Empty<string>());
            Runs.CreateAsync(default!, default).ReturnsForAnyArgs(ci =>
                new CreateDreamRunOutcome(ci.Arg<DreamRun>()));
            Instructions
                .GetUserInstructionsByKindsAsync(default!, default!, default)
                .ReturnsForAnyArgs(Array.Empty<Instruction>());
            var counter = new ContextTokenCounter(new LengthTokenizer());
            var resolver = new DreamWindowResolver(Runs, Window, counter);
            Handler = new RunDreamHandler(Runs, Instructions, resolver, new PassthroughUnitOfWork(), new TestCurrentUserAccessor(), clock);
        }
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

    private sealed class LengthTokenizer : ITokenizer
    {
        public int CountTokens(string text) => text?.Length ?? 0;
    }
}
