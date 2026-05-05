using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoDreamRunRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser)
    : IDreamRunRepository
{
    private readonly IMongoCollection<DreamRunDocument> _runs =
        database.GetCollection<DreamRunDocument>(MongoCollectionNames.DreamRuns);

    private FilterDefinition<DreamRunDocument> OwnerFilter() =>
        Builders<DreamRunDocument>.Filter.Eq(d => d.OwnerUserId, currentUser.UserId);

    public async Task<CreateDreamRunOutcome> CreateAsync(DreamRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        var session = RequireSession(nameof(CreateAsync));
        await _runs.InsertOneAsync(session, MongoDreamRunMapping.ToDocument(run), options: null, ct);
        return new CreateDreamRunOutcome(run);
    }

    public async Task<DreamRun?> GetByIdAsync(DreamRunId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var doc = session is null
            ? await _runs.Find(Builders<DreamRunDocument>.Filter.And(Builders<DreamRunDocument>.Filter.Eq(d => d.Id, id.Value), OwnerFilter())).FirstOrDefaultAsync(ct)
            : await _runs.Find(session, Builders<DreamRunDocument>.Filter.And(Builders<DreamRunDocument>.Filter.Eq(d => d.Id, id.Value), OwnerFilter())).FirstOrDefaultAsync(ct);
        return doc is null ? null : MongoDreamRunMapping.ToDomain(doc);
    }

    public async Task<IReadOnlyList<DreamRun>> ListPendingAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = Builders<DreamRunDocument>.Filter.And(
            OwnerFilter(),
            Builders<DreamRunDocument>.Filter.Eq(d => d.Status, DreamRunStatusNames.Pending));
        var docs = session is null
            ? await _runs.Find(filter).SortBy(d => d.CreatedAt).ToListAsync(ct)
            : await _runs.Find(session, filter).SortBy(d => d.CreatedAt).ToListAsync(ct);
        return docs.Select(MongoDreamRunMapping.ToDomain).ToList();
    }

    public async Task<int> GetPendingProposalsCountAsync(CancellationToken ct)
    {
        var pending = await ListPendingAsync(ct);
        return pending.Sum(r => r.PendingCount);
    }

    public async Task<IReadOnlyCollection<string>> GetProcessedIntentIdsAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = Builders<DreamRunDocument>.Filter.And(
            OwnerFilter(),
            Builders<DreamRunDocument>.Filter.Eq(d => d.Status, DreamRunStatusNames.Closed),
            Builders<DreamRunDocument>.Filter.Eq(d => d.EvidenceProcessed, true));
        var docs = session is null
            ? await _runs.Find(filter).Project(d => d.IntentRefs).ToListAsync(ct)
            : await _runs.Find(session, filter).Project(d => d.IntentRefs).ToListAsync(ct);

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var refs in docs)
        {
            foreach (var r in refs)
            {
                set.Add(r.IntentId);
            }
        }
        return set;
    }

    public async Task<IReadOnlyCollection<string>> GetLockedIntentIdsAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = Builders<DreamRunDocument>.Filter.And(
            OwnerFilter(),
            Builders<DreamRunDocument>.Filter.Eq(d => d.Status, DreamRunStatusNames.Pending));
        var docs = session is null
            ? await _runs.Find(filter).Project(d => d.IntentRefs).ToListAsync(ct)
            : await _runs.Find(session, filter).Project(d => d.IntentRefs).ToListAsync(ct);

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var refs in docs)
        {
            foreach (var r in refs)
            {
                set.Add(r.IntentId);
            }
        }
        return set;
    }

    public async Task<AddDreamProposalOutcome> AddProposalAsync(
        DreamRunId runId,
        DreamProposal proposal,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var session = RequireSession(nameof(AddProposalAsync));
        var doc = await _runs.Find(session, Builders<DreamRunDocument>.Filter.And(Builders<DreamRunDocument>.Filter.Eq(d => d.Id, runId.Value), OwnerFilter())).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            return new AddDreamProposalOutcome.RunNotFound();
        }

        var run = MongoDreamRunMapping.ToDomain(doc);
        if (run.IsClosed)
        {
            return new AddDreamProposalOutcome.RunClosed();
        }
        if (run.Proposals.Count >= DreamRun.MaxProposals)
        {
            return new AddDreamProposalOutcome.CapReached();
        }

        run.AddProposal(proposal);
        await PersistAsync(session, run, ct);
        return new AddDreamProposalOutcome.Added(run, proposal);
    }

    public async Task<ApplyDreamProposalOutcome> ApplyProposalAsync(
        DreamRunId runId,
        DreamProposalId proposalId,
        string finalRule,
        int appliedInstructionVersion,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var session = RequireSession(nameof(ApplyProposalAsync));
        var doc = await _runs.Find(session, Builders<DreamRunDocument>.Filter.And(Builders<DreamRunDocument>.Filter.Eq(d => d.Id, runId.Value), OwnerFilter())).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            return new ApplyDreamProposalOutcome.RunNotFound();
        }

        var run = MongoDreamRunMapping.ToDomain(doc);
        var domainResult = run.ApplyProposal(proposalId, finalRule, appliedInstructionVersion, now);
        switch (domainResult)
        {
            case DreamProposalDecisionResult.RunAlreadyClosed:
                return new ApplyDreamProposalOutcome.RunAlreadyClosed();
            case DreamProposalDecisionResult.ProposalNotFound:
                return new ApplyDreamProposalOutcome.ProposalNotFound();
            case DreamProposalDecisionResult.AlreadyDecided ad:
                return new ApplyDreamProposalOutcome.AlreadyDecided(ad.Proposal.Decision);
            case DreamProposalDecisionResult.Decided decided:
                await PersistAsync(session, run, ct);
                return new ApplyDreamProposalOutcome.Applied(run, decided.Proposal, decided.AutoClosed);
            default:
                throw new InvalidOperationException($"Unhandled decision result: {domainResult.GetType().Name}");
        }
    }

    public async Task<SkipDreamProposalOutcome> SkipProposalAsync(
        DreamRunId runId,
        DreamProposalId proposalId,
        string reason,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var session = RequireSession(nameof(SkipProposalAsync));
        var doc = await _runs.Find(session, Builders<DreamRunDocument>.Filter.And(Builders<DreamRunDocument>.Filter.Eq(d => d.Id, runId.Value), OwnerFilter())).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            return new SkipDreamProposalOutcome.RunNotFound();
        }

        var run = MongoDreamRunMapping.ToDomain(doc);
        var domainResult = run.SkipProposal(proposalId, reason, now);
        switch (domainResult)
        {
            case DreamProposalDecisionResult.RunAlreadyClosed:
                return new SkipDreamProposalOutcome.RunAlreadyClosed();
            case DreamProposalDecisionResult.ProposalNotFound:
                return new SkipDreamProposalOutcome.ProposalNotFound();
            case DreamProposalDecisionResult.AlreadyDecided ad:
                return new SkipDreamProposalOutcome.AlreadyDecided(ad.Proposal.Decision);
            case DreamProposalDecisionResult.Decided decided:
                await PersistAsync(session, run, ct);
                return new SkipDreamProposalOutcome.Skipped(run, decided.Proposal, decided.AutoClosed);
            default:
                throw new InvalidOperationException($"Unhandled decision result: {domainResult.GetType().Name}");
        }
    }

    public async Task<CloseDreamRunOutcome> CloseAsync(
        DreamRunId runId,
        bool? releaseEvidenceOverride,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var session = RequireSession(nameof(CloseAsync));
        var doc = await _runs.Find(session, Builders<DreamRunDocument>.Filter.And(Builders<DreamRunDocument>.Filter.Eq(d => d.Id, runId.Value), OwnerFilter())).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            return new CloseDreamRunOutcome.NotFound();
        }

        var run = MongoDreamRunMapping.ToDomain(doc);
        var result = run.Close(releaseEvidenceOverride, now);
        if (result is DreamRunCloseResult.AlreadyClosed)
        {
            return new CloseDreamRunOutcome.AlreadyClosed();
        }

        await PersistAsync(session, run, ct);
        return new CloseDreamRunOutcome.Closed(run);
    }

    private async Task PersistAsync(IClientSessionHandle session, DreamRun run, CancellationToken ct)
    {
        var replaced = MongoDreamRunMapping.ToDocument(run);
        await _runs.ReplaceOneAsync(
            session,
            Builders<DreamRunDocument>.Filter.And(
                Builders<DreamRunDocument>.Filter.Eq(d => d.Id, run.Id.Value),
                OwnerFilter()),
            replaced,
            options: (ReplaceOptions?)null,
            ct);
    }

    private IClientSessionHandle RequireSession(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoDreamRunRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
