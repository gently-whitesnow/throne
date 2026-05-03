using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
public class MongoDreamRunRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет DreamRun + индекс evidence_refs позволяет lookup")]
    public async Task Create_persists_run_and_evidence_refs()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var run = NewRun();
        await uow.ExecuteAsync(ct => repo.CreateAsync(run, ct), CancellationToken.None);

        var stored = await db.GetCollection<DreamRunDocument>(MongoCollectionNames.DreamRuns)
            .Find(d => d.Id == run.Id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(DreamRunStatusNames.Pending);
        stored.EvidenceRefs.Should().HaveCount(1);
        stored.EvidenceRefs[0].Kind.Should().Be(EvidenceKindNames.Review);
    }

    [Fact(DisplayName = "Apply последнего proposal: status=closed, EvidenceProcessed=true")]
    public async Task Apply_last_proposal_auto_closes_and_processes_evidence()
    {
        var (_, repo, uow) = await NewScopeAsync();
        var run = NewRun();
        var proposal = NewProposal();
        await uow.ExecuteAsync(ct => repo.CreateAsync(run, ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => repo.AddProposalAsync(run.Id, proposal, ct), CancellationToken.None);

        var outcome = await uow.ExecuteAsync(
            ct => repo.ApplyProposalAsync(run.Id, proposal.Id, "правило", appliedInstructionVersion: 9, Now.AddMinutes(1), ct),
            CancellationToken.None);

        var applied = outcome.Should().BeOfType<ApplyDreamProposalOutcome.Applied>().Subject;
        applied.AutoClosed.Should().BeTrue();
        applied.Run.IsClosed.Should().BeTrue();
        applied.Run.EvidenceProcessed.Should().BeTrue();
        applied.Proposal.AppliedInstructionVersion.Should().Be(9);
    }

    [Fact(DisplayName = "Pending proposal другого run попадает в LockedEvidence; processed после close")]
    public async Task Locked_then_processed_evidence_lookup()
    {
        var (_, repo, uow) = await NewScopeAsync();
        var run = NewRun();
        await uow.ExecuteAsync(ct => repo.CreateAsync(run, ct), CancellationToken.None);

        var locked = await repo.GetLockedEvidenceAsync(CancellationToken.None);
        locked.Should().Contain((EvidenceKindNames.Review, "rev-1"));

        await uow.ExecuteAsync(
            ct => repo.CloseAsync(run.Id, releaseEvidenceOverride: false, Now.AddMinutes(2), ct),
            CancellationToken.None);

        var processed = await repo.GetProcessedEvidenceAsync(CancellationToken.None);
        processed.Should().Contain((EvidenceKindNames.Review, "rev-1"));
    }

    [Fact(DisplayName = "Manual close пустого run с release_evidence=true → evidence не processed")]
    public async Task Empty_run_close_releases_evidence()
    {
        var (_, repo, uow) = await NewScopeAsync();
        var run = NewRun();
        await uow.ExecuteAsync(ct => repo.CreateAsync(run, ct), CancellationToken.None);

        await uow.ExecuteAsync(
            ct => repo.CloseAsync(run.Id, releaseEvidenceOverride: null, Now.AddMinutes(2), ct),
            CancellationToken.None);

        var processed = await repo.GetProcessedEvidenceAsync(CancellationToken.None);
        processed.Should().BeEmpty();
    }

    private async Task<(IMongoDatabase Db, MongoDreamRunRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoDreamRunRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (db, repo, uow);
    }

    private static DreamRun NewRun() => DreamRun.Create(
        DreamRunId.New(),
        Now.AddDays(-7),
        Now.AddMinutes(-30),
        readinessScore: 12,
        new EvidenceCounts(1, 0, 0, 0, 0, 0, 0),
        [EvidenceRef.Create(EvidenceKindNames.Review, "rev-1")],
        OmittedEvidenceCounts.Zero,
        Now);

    private static DreamProposal NewProposal() => DreamProposal.Create(
        DreamProposalId.New(),
        targetInstructionId: "instr-1",
        targetKind: "work",
        baseInstructionVersion: 5,
        proposedRule: "Не делай unrelated refactor.",
        evidenceSummary: "Reviews указывают на расширение скоупа.",
        evidenceRefs: [EvidenceRef.Create(EvidenceKindNames.Review, "rev-1")],
        rationale: "См. связанные reviews.",
        severity: DreamProposalSeverityNames.High);
}
