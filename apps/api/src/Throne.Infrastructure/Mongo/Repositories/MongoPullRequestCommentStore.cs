using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

/// <summary>
/// Mongo-backed <see cref="IPullRequestCommentRepository"/> owned by T-10. Sessions are
/// resolved through <see cref="MongoSessionAccessor"/> so writes participate in the
/// caller's <see cref="IUnitOfWork.ExecuteAsync"/> transaction; the
/// <c>(binding_id, upstream_id)</c> unique index handles concurrent racers (manual
/// sync vs background poll) at the storage layer instead of in C#.
/// </summary>
internal sealed class MongoPullRequestCommentStore(
    IMongoDatabase database,
    MongoSessionAccessor sessions) : IPullRequestCommentRepository
{
    private readonly IMongoCollection<PullRequestCommentDocument> _comments =
        database.GetCollection<PullRequestCommentDocument>(MongoCollectionNames.PullRequestComments);

    public async Task<PersistPullRequestCommentsOutcome> PersistNewAsync(
        IntentRepositoryBinding binding,
        IReadOnlyList<PullRequestCommentRecord> candidates,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(candidates);

        var inserted = candidates.Count == 0
            ? []
            : await InsertNewAsync(binding.Id, candidates, ct);

        var all = await ListByBindingAsync(binding.Id, ct);
        return new PersistPullRequestCommentsOutcome(binding, inserted, all);
    }

    public async Task<IReadOnlyList<PullRequestCommentRecord>> ListByBindingAsync(
        BindingId bindingId,
        CancellationToken ct)
    {
        var filter = Builders<PullRequestCommentDocument>.Filter.Eq(d => d.BindingId, bindingId.Value);
        var session = sessions.Current;
        var find = session is null ? _comments.Find(filter) : _comments.Find(session, filter);
        var docs = await find
            .SortBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    private async Task<IReadOnlyList<PullRequestCommentRecord>> InsertNewAsync(
        BindingId bindingId,
        IReadOnlyList<PullRequestCommentRecord> candidates,
        CancellationToken ct)
    {
        var existing = await ExistingUpstreamIdsAsync(bindingId, candidates.Select(c => c.UpstreamId), ct);
        var fresh = candidates.Where(c => !existing.Contains(c.UpstreamId)).ToList();
        if (fresh.Count == 0)
        {
            return [];
        }

        var docs = fresh.Select(ToDocument).ToList();
        var session = sessions.Current;
        var inserted = new List<PullRequestCommentRecord>(fresh.Count);
        for (var i = 0; i < docs.Count; i++)
        {
            try
            {
                if (session is null)
                {
                    await _comments.InsertOneAsync(docs[i], options: null, ct);
                }
                else
                {
                    await _comments.InsertOneAsync(session, docs[i], options: null, ct);
                }
                inserted.Add(fresh[i]);
            }
            catch (MongoWriteException ex) when (IsDuplicateKey(ex))
            {
                // Lost the race against a sibling sync that already stored this id.
                // Skip silently so the «inserted» list stays an honest fanout subset.
            }
        }
        return inserted;
    }

    private async Task<HashSet<string>> ExistingUpstreamIdsAsync(
        BindingId bindingId,
        IEnumerable<string> upstreamIds,
        CancellationToken ct)
    {
        var ids = upstreamIds.ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        var fb = Builders<PullRequestCommentDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.BindingId, bindingId.Value),
            fb.In(d => d.UpstreamId, ids));
        var session = sessions.Current;
        var find = session is null ? _comments.Find(filter) : _comments.Find(session, filter);
        var docs = await find.Project(d => d.UpstreamId).ToListAsync(ct);
        return new HashSet<string>(docs, StringComparer.Ordinal);
    }

    private static PullRequestCommentDocument ToDocument(PullRequestCommentRecord record) => new()
    {
        Id = $"{record.BindingId.Value}:{record.UpstreamId}",
        BindingId = record.BindingId.Value,
        IntentId = record.IntentId.Value,
        UpstreamId = record.UpstreamId,
        AuthorLogin = record.AuthorLogin,
        AuthorAvatarUrl = record.AuthorAvatarUrl,
        Body = record.Body,
        HtmlUrl = record.HtmlUrl,
        Path = record.Path,
        CreatedAt = record.CreatedAt.UtcDateTime,
        UpdatedAt = record.UpdatedAt?.UtcDateTime,
        ObservedAt = record.ObservedAt.UtcDateTime,
    };

    private static PullRequestCommentRecord ToDomain(PullRequestCommentDocument doc) => new(
        BindingId: new BindingId(doc.BindingId),
        IntentId: new IntentId(doc.IntentId),
        UpstreamId: doc.UpstreamId,
        AuthorLogin: doc.AuthorLogin,
        Body: doc.Body,
        CreatedAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        ObservedAt: DateTime.SpecifyKind(doc.ObservedAt, DateTimeKind.Utc),
        AuthorAvatarUrl: doc.AuthorAvatarUrl,
        HtmlUrl: doc.HtmlUrl,
        Path: doc.Path,
        UpdatedAt: doc.UpdatedAt is null
            ? null
            : DateTime.SpecifyKind(doc.UpdatedAt.Value, DateTimeKind.Utc));

    private static bool IsDuplicateKey(MongoWriteException ex) =>
        ex.WriteError?.Category == ServerErrorCategory.DuplicateKey;
}
