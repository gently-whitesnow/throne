using FluentAssertions;
using NSubstitute;
using Throne.Application.DreamRuns;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class CloseEmptyDreamRunHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Run без proposals закрывается с release_evidence=true по умолчанию")]
    public async Task Empty_run_closes_with_release_default()
    {
        var fixture = new Fixture();
        var run = SampleRun(includeProposal: false);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);
        fixture.Runs.CloseAsync(default, default, default, default).ReturnsForAnyArgs(new CloseDreamRunOutcome.Closed(run));

        var result = await fixture.Handler.HandleAsync(new CloseEmptyDreamRunCommand(run.Id.Value, ReleaseEvidence: null), CancellationToken.None);

        result.Should().BeSameAs(run);
        await fixture.Runs.Received(1).CloseAsync(
            run.Id,
            true,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run с proposals → dream.run.has_proposals (агент не может маскировать свои предложения)")]
    public async Task Run_with_proposals_rejected()
    {
        var fixture = new Fixture();
        var run = SampleRun(includeProposal: true);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);

        var act = () => fixture.Handler.HandleAsync(new CloseEmptyDreamRunCommand(run.Id.Value, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.DreamRunHasProposals);
        await fixture.Runs.DidNotReceive().CloseAsync(default, default, default, default);
    }

    [Fact(DisplayName = "Run уже закрыт → dream.run.already_closed")]
    public async Task Already_closed_rejected()
    {
        var fixture = new Fixture();
        var run = SampleRun(includeProposal: false);
        run.Close(releaseEvidenceOverride: true, Now);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);

        var act = () => fixture.Handler.HandleAsync(new CloseEmptyDreamRunCommand(run.Id.Value, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.DreamRunAlreadyClosed);
    }

    [Fact(DisplayName = "Run не найден → dream.run.not_found")]
    public async Task Not_found()
    {
        var fixture = new Fixture();
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs((DreamRun?)null);

        var act = () => fixture.Handler.HandleAsync(new CloseEmptyDreamRunCommand("missing", null), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.DreamRunNotFound);
    }

    private static DreamRun SampleRun(bool includeProposal)
    {
        var run = DreamRun.Create(
            DreamRunId.New(),
            ownerUserId: "user-1",
            tokenCount: 100,
            [IntentRef.Create("intent-1", 100, Now)],
            Now);
        if (includeProposal)
        {
            run.AddProposal(DreamProposal.Create(
                DreamProposalId.New(),
                "inst-work",
                Throne.Domain.Instructions.InstructionKindNames.Work,
                baseInstructionVersion: 1,
                proposedRule: "rule",
                evidenceSummary: "intents:1",
                intentRefs: [IntentRef.Create("intent-1", 100, Now)],
                rationale: "r",
                severity: DreamProposalSeverityNames.High));
        }
        return run;
    }

    private sealed class Fixture
    {
        public IDreamRunRepository Runs { get; } = Substitute.For<IDreamRunRepository>();
        public CloseEmptyDreamRunHandler Handler { get; }

        public Fixture()
        {
            Handler = new CloseEmptyDreamRunHandler(Runs, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));
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
