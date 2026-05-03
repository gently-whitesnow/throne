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
    private static readonly DateTimeOffset WindowStart = Now.AddDays(-3);
    private static readonly DateTimeOffset WindowEnd = Now.AddMinutes(-30);

    [Fact(DisplayName = "Empty readiness возвращает not_enough_context без создания run")]
    public async Task Empty_readiness_returns_not_enough_context()
    {
        var fixture = new Fixture();
        fixture.Evidence.CollectAsync(default, default, default, default).ReturnsForAnyArgs([]);

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
        var existing = SampleRun(fixture, createdAt: Now.AddHours(-2));
        fixture.Runs.ListPendingAsync(default).ReturnsForAnyArgs([existing]);
        fixture.Evidence.CollectAsync(default, default, default, default).ReturnsForAnyArgs(new EvidenceItemRecord[]
        {
            new(EvidenceKindNames.Review, "rev-a", Now.AddHours(-1), SessionId: null, HighSeverity: true),
            new(EvidenceKindNames.Review, "rev-b", Now.AddHours(-1), SessionId: null, HighSeverity: true),
        });

        var result = await fixture.Handler.HandleAsync(new RunDreamCommand(null), CancellationToken.None);

        result.Status.Should().Be(RunDreamResultStatuses.ExistingPending);
        result.DreamRun!.Run.Id.Should().Be(existing.Id);
        await fixture.Runs.DidNotReceive().CreateAsync(Arg.Any<DreamRun>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Готовый score → создаёт run, отдаёт evidence_summary с existing_learned_rules_by_kind")]
    public async Task Ready_path_creates_run_with_summary()
    {
        var fixture = new Fixture();
        fixture.Evidence.CollectAsync(default, default, default, default).ReturnsForAnyArgs(new EvidenceItemRecord[]
        {
            new(EvidenceKindNames.Review, "rev-a", Now.AddHours(-2), SessionId: null, HighSeverity: false),
            new(EvidenceKindNames.Review, "rev-b", Now.AddHours(-3), SessionId: null, HighSeverity: false),
            new(EvidenceKindNames.Qa, "qa-a", Now.AddHours(-4), SessionId: null, HighSeverity: false),
        });
        fixture.Instructions
            .GetUserInstructionsByKindsAsync(MvpUser.Id, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([SeededWorkInstruction()]);

        var result = await fixture.Handler.HandleAsync(new RunDreamCommand("auto"), CancellationToken.None);

        result.Status.Should().Be(RunDreamResultStatuses.Created);
        result.DreamRun.Should().NotBeNull();
        result.DreamRun!.EvidenceRefs.Should().HaveCount(3);
        result.DreamRun.EvidenceSummary.SuggestedTargetKinds.Should().Contain(InstructionKindNames.Work);
        result.DreamRun.EvidenceSummary.SuggestedTargetKinds.Should().Contain(InstructionKindNames.Interview);
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

    private static DreamRun SampleRun(Fixture fixture, DateTimeOffset createdAt) => DreamRun.Create(
        DreamRunId.New(),
        WindowStart,
        WindowEnd,
        readinessScore: 10,
        new EvidenceCounts(2, 0, 0, 0, 0, 0, 0),
        [
            new EvidenceRef(EvidenceKindNames.Review, "rev-a"),
            new EvidenceRef(EvidenceKindNames.Review, "rev-b"),
        ],
        OmittedEvidenceCounts.Zero,
        createdAt);

    private static Instruction SeededWorkInstruction() => Instruction.Restore(
        new InstructionId("inst-work"),
        InstructionScopeNames.User,
        userId: MvpUser.Id,
        kind: InstructionKindNames.Work,
        text: "# Work\n\n## Learned rules\n\n- Already learned thing\n",
        currentVersion: 3,
        createdAt: Now.AddDays(-30),
        updatedAt: Now.AddDays(-1));

    private sealed class Fixture
    {
        public IDreamRunRepository Runs { get; } = Substitute.For<IDreamRunRepository>();
        public IEvidenceQueries Evidence { get; } = Substitute.For<IEvidenceQueries>();
        public IInstructionRepository Instructions { get; } = Substitute.For<IInstructionRepository>();
        public RunDreamHandler Handler { get; }

        public Fixture()
        {
            var clock = new FakeTimeProvider(Now);
            var options = new DreamOptions
            {
                SafetyLagMinutes = 30,
                MaxWindowDays = 90,
                Thresholds = new DreamReadinessThresholds { Ready = 10, Rich = 40 },
            };
            Runs.GetMostRecentClosedAsync(default).ReturnsForAnyArgs((DreamRun?)null);
            Runs.ListPendingAsync(default).ReturnsForAnyArgs(Array.Empty<DreamRun>());
            Runs.GetProcessedEvidenceAsync(default).ReturnsForAnyArgs(Array.Empty<(string, string)>());
            Runs.GetLockedEvidenceAsync(default).ReturnsForAnyArgs(Array.Empty<(string, string)>());
            Runs.CreateAsync(default!, default).ReturnsForAnyArgs(ci =>
                new CreateDreamRunOutcome(ci.Arg<DreamRun>()));
            Instructions
                .GetUserInstructionsByKindsAsync(default!, default!, default)
                .ReturnsForAnyArgs(Array.Empty<Instruction>());
            var resolver = new DreamWindowResolver(Runs, Evidence, options, clock);
            Handler = new RunDreamHandler(Runs, Instructions, resolver, options, new PassthroughUnitOfWork(), clock);
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
}
