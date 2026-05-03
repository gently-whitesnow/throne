using FluentAssertions;
using Throne.Domain.DreamRuns;

namespace Throne.Domain.Tests.DreamRuns;

public class DreamRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly EvidenceRef HighRef = EvidenceRef.Create(EvidenceKindNames.Review, "rev-1");
    private static readonly EvidenceRef MediumRefA = EvidenceRef.Create(EvidenceKindNames.Qa, "qa-1");
    private static readonly EvidenceRef MediumRefB = EvidenceRef.Create(EvidenceKindNames.Qa, "qa-2");

    [Fact(DisplayName = "DreamRun.Create — pending status, без proposals, окно валидно")]
    public void Create_initializes_pending_run()
    {
        var run = NewRun();
        run.Status.Should().Be(DreamRunStatusNames.Pending);
        run.Proposals.Should().BeEmpty();
        run.IsClosed.Should().BeFalse();
        run.EvidenceProcessed.Should().BeFalse();
    }

    [Fact(DisplayName = "AddProposal: high-severity требует ≥1 evidence ref")]
    public void HighSeverity_requires_one_ref()
    {
        var run = NewRun();
        var proposal = NewProposal(DreamProposalSeverityNames.High, [HighRef]);
        run.AddProposal(proposal);
        run.Proposals.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Medium severity требует ≥2 evidence refs — ArgumentException иначе")]
    public void Medium_requires_two_refs()
    {
        var act = () => NewProposal(DreamProposalSeverityNames.Medium, [MediumRefA]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "AddProposal: cap = 5 → InvalidOperation на шестой proposal")]
    public void Cap_at_five_proposals()
    {
        var run = NewRun();
        for (var i = 0; i < DreamRun.MaxProposals; i++)
        {
            run.AddProposal(NewProposal(DreamProposalSeverityNames.High, [HighRef]));
        }
        var act = () => run.AddProposal(NewProposal(DreamProposalSeverityNames.High, [HighRef]));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Auto-close: применение последнего pending → status=closed, EvidenceProcessed=true")]
    public void Apply_last_pending_auto_closes_run()
    {
        var run = NewRun();
        var p = NewProposal(DreamProposalSeverityNames.High, [HighRef]);
        run.AddProposal(p);

        var result = run.ApplyProposal(p.Id, "правило", appliedInstructionVersion: 7, Now.AddMinutes(1));

        result.Should().BeOfType<DreamProposalDecisionResult.Decided>();
        var decided = (DreamProposalDecisionResult.Decided)result;
        decided.AutoClosed.Should().BeTrue();
        run.IsClosed.Should().BeTrue();
        run.EvidenceProcessed.Should().BeTrue();
        p.Decision.Should().Be(DreamProposalDecisionNames.Applied);
        p.AppliedInstructionVersion.Should().Be(7);
    }

    [Fact(DisplayName = "Skip: ≥1 pending остаётся → run не закрывается автоматически")]
    public void Skip_one_of_two_keeps_pending_open()
    {
        var run = NewRun();
        var p1 = NewProposal(DreamProposalSeverityNames.Medium, [MediumRefA, MediumRefB]);
        var p2 = NewProposal(DreamProposalSeverityNames.High, [HighRef]);
        run.AddProposal(p1);
        run.AddProposal(p2);

        var result = run.SkipProposal(p1.Id, "не подходит — повторяет существующее", Now);
        var decided = result.Should().BeOfType<DreamProposalDecisionResult.Decided>().Subject;
        decided.AutoClosed.Should().BeFalse();
        run.IsClosed.Should().BeFalse();
        run.PendingCount.Should().Be(1);
    }

    [Fact(DisplayName = "Manual close пустого run: default release_evidence=true → EvidenceProcessed=false")]
    public void Manual_close_empty_run_defaults_to_release()
    {
        var run = NewRun();
        var result = run.Close(releaseEvidenceOverride: null, Now);
        var closed = result.Should().BeOfType<DreamRunCloseResult.Closed>().Subject;
        closed.EvidenceProcessed.Should().BeFalse();
        run.IsClosed.Should().BeTrue();
        run.EvidenceRefs.Should().BeEmpty();
    }

    [Fact(DisplayName = "Manual close non-empty run: default release_evidence=false → EvidenceProcessed=true")]
    public void Manual_close_non_empty_run_consumes_evidence()
    {
        var run = NewRun();
        run.AddProposal(NewProposal(DreamProposalSeverityNames.High, [HighRef]));
        var result = run.Close(releaseEvidenceOverride: null, Now);
        var closed = result.Should().BeOfType<DreamRunCloseResult.Closed>().Subject;
        closed.EvidenceProcessed.Should().BeTrue();
        run.EvidenceRefs.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Skip: повторное решение → AlreadyDecided")]
    public void Skip_already_decided_returns_already_decided()
    {
        var run = NewRun();
        var p = NewProposal(DreamProposalSeverityNames.Medium, [MediumRefA, MediumRefB]);
        run.AddProposal(p);
        run.AddProposal(NewProposal(DreamProposalSeverityNames.High, [HighRef]));
        run.SkipProposal(p.Id, "duplicate of rule X", Now);

        var second = run.SkipProposal(p.Id, "another reason", Now);
        second.Should().BeOfType<DreamProposalDecisionResult.AlreadyDecided>();
    }

    private static DreamRun NewRun() => DreamRun.Create(
        DreamRunId.New(),
        Now.AddDays(-7),
        Now.AddMinutes(-30),
        readinessScore: 12,
        new EvidenceCounts(1, 2, 0, 0, 0, 0, 0),
        [HighRef, MediumRefA, MediumRefB],
        OmittedEvidenceCounts.Zero,
        Now);

    private static DreamProposal NewProposal(string severity, IReadOnlyList<EvidenceRef> refs) => DreamProposal.Create(
        DreamProposalId.New(),
        targetInstructionId: "instr-1",
        targetKind: "work",
        baseInstructionVersion: 5,
        proposedRule: "Не делай unrelated refactor в hotfix-PR.",
        evidenceSummary: "Reviews указывают на расширение скоупа.",
        evidenceRefs: refs,
        rationale: "См. связанные reviews — pattern повторяется.",
        severity: severity);
}
