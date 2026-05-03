using FluentAssertions;
using NSubstitute;
using Throne.Application.DreamRuns;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.DreamRuns;

public class ProposeDreamRuleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Happy path → AddProposalAsync вызывается, возвращён proposal_id и status=pending")]
    public async Task Happy_path_creates_proposal()
    {
        var fixture = new Fixture();
        var run = SampleRun(refs: [
            new EvidenceRef(EvidenceKindNames.Review, "rev-1"),
            new EvidenceRef(EvidenceKindNames.Review, "rev-2"),
        ]);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);
        fixture.Instructions
            .GetUserInstructionsByKindsAsync(MvpUser.Id, Arg.Is<IReadOnlyList<string>>(l => l.Contains(InstructionKindNames.Work)), Arg.Any<CancellationToken>())
            .Returns([SampleInstruction()]);
        DreamProposal? captured = null;
        fixture.Runs
            .AddProposalAsync(default, default!, default)
            .ReturnsForAnyArgs(ci =>
            {
                captured = ci.Arg<DreamProposal>();
                return new AddDreamProposalOutcome.Added(run, captured);
            });

        var result = await fixture.Handler.HandleAsync(
            new ProposeDreamRuleCommand(
                run.Id.Value,
                InstructionKindNames.Work,
                "Always run verify before mark_ready_for_review.",
                [
                    new EvidenceRef(EvidenceKindNames.Review, "rev-1"),
                    new EvidenceRef(EvidenceKindNames.Review, "rev-2"),
                ],
                "User complained twice that we skipped the gate.",
                DreamProposalSeverityNames.Medium),
            CancellationToken.None);

        result.Status.Should().Be(DreamProposalDecisionNames.Pending);
        captured!.TargetInstructionId.Should().Be("inst-work");
        captured.BaseInstructionVersion.Should().Be(3);
        captured.EvidenceRefs.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Disallowed target_kind ('dream') → validation.failed")]
    public async Task Disallowed_target_kind_rejected()
    {
        var fixture = new Fixture();

        var act = () => fixture.Handler.HandleAsync(
            new ProposeDreamRuleCommand(
                "run", InstructionKindNames.Dream, "rule",
                [new EvidenceRef(EvidenceKindNames.Review, "x")],
                "rationale",
                DreamProposalSeverityNames.High),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "evidence_refs не подмножество run → dream.proposal.evidence_unknown")]
    public async Task Evidence_must_be_subset()
    {
        var fixture = new Fixture();
        var run = SampleRun(refs: [new EvidenceRef(EvidenceKindNames.Review, "in-run")]);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);

        var act = () => fixture.Handler.HandleAsync(
            new ProposeDreamRuleCommand(
                run.Id.Value, InstructionKindNames.Work, "rule",
                [new EvidenceRef(EvidenceKindNames.Review, "outside")],
                "rationale", DreamProposalSeverityNames.High),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.DreamProposalEvidenceUnknown);
    }

    [Fact(DisplayName = "Severity=medium с одним ref → validation.failed (минимум 2)")]
    public async Task Severity_demands_minimum_evidence()
    {
        var fixture = new Fixture();
        var run = SampleRun(refs: [new EvidenceRef(EvidenceKindNames.Review, "rev-1")]);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);

        var act = () => fixture.Handler.HandleAsync(
            new ProposeDreamRuleCommand(
                run.Id.Value, InstructionKindNames.Work, "rule",
                [new EvidenceRef(EvidenceKindNames.Review, "rev-1")],
                "rationale", DreamProposalSeverityNames.Medium),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Run уже имеет 5 предложений → dream.proposal.cap_reached")]
    public async Task Cap_reached_rejected()
    {
        var fixture = new Fixture();
        var run = SampleRunWithProposals(count: DreamRun.MaxProposals);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);

        var act = () => fixture.Handler.HandleAsync(
            new ProposeDreamRuleCommand(
                run.Id.Value, InstructionKindNames.Work, "rule",
                [new EvidenceRef(EvidenceKindNames.Review, "rev-1")],
                "rationale", DreamProposalSeverityNames.High),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.DreamProposalCapReached);
    }

    [Fact(DisplayName = "Run закрыт → dream.run.already_closed")]
    public async Task Closed_run_rejected()
    {
        var fixture = new Fixture();
        var run = SampleRun(refs: [new EvidenceRef(EvidenceKindNames.Review, "rev-1")]);
        run.Close(releaseEvidenceOverride: true, Now);
        fixture.Runs.GetByIdAsync(default, default).ReturnsForAnyArgs(run);

        var act = () => fixture.Handler.HandleAsync(
            new ProposeDreamRuleCommand(
                run.Id.Value, InstructionKindNames.Work, "rule",
                [new EvidenceRef(EvidenceKindNames.Review, "rev-1")],
                "rationale", DreamProposalSeverityNames.High),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.DreamRunAlreadyClosed);
    }

    private static DreamRun SampleRun(IReadOnlyList<EvidenceRef> refs) => DreamRun.Create(
        DreamRunId.New(),
        Now.AddDays(-1),
        Now.AddMinutes(-30),
        readinessScore: 10,
        new EvidenceCounts(refs.Count, 0, 0, 0, 0, 0, 0),
        refs,
        OmittedEvidenceCounts.Zero,
        Now);

    private static DreamRun SampleRunWithProposals(int count)
    {
        var run = SampleRun([new EvidenceRef(EvidenceKindNames.Review, "rev-1")]);
        for (var i = 0; i < count; i++)
        {
            run.AddProposal(DreamProposal.Create(
                DreamProposalId.New(),
                "inst-work",
                InstructionKindNames.Work,
                baseInstructionVersion: 1,
                proposedRule: $"rule {i}",
                evidenceSummary: "review:1",
                evidenceRefs: [new EvidenceRef(EvidenceKindNames.Review, "rev-1")],
                rationale: "r",
                severity: DreamProposalSeverityNames.High));
        }
        return run;
    }

    private static Instruction SampleInstruction() => Instruction.Restore(
        new InstructionId("inst-work"),
        InstructionScopeNames.User,
        userId: MvpUser.Id,
        kind: InstructionKindNames.Work,
        text: "# Work\n",
        currentVersion: 3,
        createdAt: Now.AddDays(-10),
        updatedAt: Now.AddDays(-1));

    private sealed class Fixture
    {
        public IDreamRunRepository Runs { get; } = Substitute.For<IDreamRunRepository>();
        public IInstructionRepository Instructions { get; } = Substitute.For<IInstructionRepository>();
        public ProposeDreamRuleHandler Handler { get; }

        public Fixture()
        {
            Handler = new ProposeDreamRuleHandler(Runs, Instructions, new PassthroughUnitOfWork());
        }
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
