using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core latest-wins upsert per <c>(binding_id, type)</c> for pull request artifacts
/// (ADR-0031). UNIQUE on the pair lets the create/insert race retry as an update; the
/// outcome carries the standard <c>pull_request.artifact_updated</c> event regardless of
/// which path won, so downstream realtime fan-out is uniform.
/// </summary>
internal sealed class EfPullRequestArtifactStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IPullRequestArtifactRepository
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public async Task<WritePullRequestArtifactOutcome> UpsertAsync(
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        string? headSha,
        ReviewRecommendation? reviewRecommendation,
        CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(UpsertAsync));
        var existing = await FindRowAsync(ctx, bindingId, type, ct);
        return existing is null
            ? await CreateAsync(ctx, bindingId, pullRequestNumber, type, render, content, summary, source,
                sourceRefs, producedAt, headSha, reviewRecommendation, ct)
            : await UpdateAsync(ctx, existing, pullRequestNumber, render, content, summary, source,
                sourceRefs, producedAt, headSha, reviewRecommendation, ct);
    }

    public Task<PullRequestArtifact?> GetAsync(BindingId bindingId, string type, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var row = await FindRowAsync(ctx, bindingId, type, c);
            return row is null ? null : PullRequestArtifactRowMapper.ToDomain(row);
        }, ct);

    public Task<IReadOnlyList<PullRequestArtifact>> ListAsync(BindingId bindingId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<PullRequestArtifact>>(async (ctx, c) =>
        {
            var wire = bindingId.Value;
            var rows = await ctx.Set<PullRequestArtifactRow>().AsNoTracking()
                .Where(r => r.BindingId == wire)
                .OrderBy(r => r.Type)
                .ToListAsync(c);
            return rows.Select(PullRequestArtifactRowMapper.ToDomain).ToList();
        }, ct);

    private static async Task<WritePullRequestArtifactOutcome> CreateAsync(
        ThroneDbContext ctx,
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        string? headSha,
        ReviewRecommendation? reviewRecommendation,
        CancellationToken ct)
    {
        var artifact = PullRequestArtifact.Create(
            PullRequestArtifactId.New(), bindingId, pullRequestNumber, type, render, content,
            summary, source, sourceRefs, producedAt, headSha, reviewRecommendation);
        ctx.Set<PullRequestArtifactRow>().Add(PullRequestArtifactRowMapper.ToRow(artifact));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return new WritePullRequestArtifactOutcome(artifact, Created: true);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            DetachInserts(ctx);
            var raced = await FindRowAsync(ctx, bindingId, type, ct)
                ?? throw new InvalidOperationException(
                    $"Pull-request artifact ({bindingId.Value}, {type}) UNIQUE conflict but re-read returned null.", ex);
            return await UpdateAsync(ctx, raced, pullRequestNumber, render, content, summary, source,
                sourceRefs, producedAt, headSha, reviewRecommendation, ct);
        }
    }

    private static async Task<WritePullRequestArtifactOutcome> UpdateAsync(
        ThroneDbContext ctx,
        PullRequestArtifactRow existing,
        int pullRequestNumber,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        string? headSha,
        ReviewRecommendation? reviewRecommendation,
        CancellationToken ct)
    {
        var artifact = PullRequestArtifactRowMapper.ToDomain(existing);
        artifact.Replace(pullRequestNumber, render, content, summary, source, sourceRefs, producedAt, headSha, reviewRecommendation);

        var id = existing.Id;
        var newSourceRefs = artifact.SourceRefs.ToList();
        var newReview = PullRequestArtifactRowMapper.ToPayload(artifact.ReviewRecommendation);
        var newProducedAt = artifact.ProducedAt;
        var newHeadSha = artifact.HeadSha;
        var newRender = artifact.Render;
        var newContent = artifact.Content;
        var newSummary = artifact.Summary;
        var newSource = artifact.Source;
        var newPullRequestNumber = artifact.PullRequestNumber;

        await ctx.Set<PullRequestArtifactRow>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.PullRequestNumber, newPullRequestNumber)
                .SetProperty(r => r.Render, newRender)
                .SetProperty(r => r.Content, newContent)
                .SetProperty(r => r.Summary, newSummary)
                .SetProperty(r => r.Source, newSource)
                .SetProperty(r => r.SourceRefs, newSourceRefs)
                .SetProperty(r => r.ProducedAt, newProducedAt)
                .SetProperty(r => r.HeadSha, newHeadSha)
                .SetProperty(r => r.ReviewRecommendation, newReview), ct);

        return new WritePullRequestArtifactOutcome(artifact, Created: false);
    }

    private static Task<PullRequestArtifactRow?> FindRowAsync(
        ThroneDbContext ctx, BindingId bindingId, string type, CancellationToken ct)
    {
        var bid = bindingId.Value;
        return ctx.Set<PullRequestArtifactRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.BindingId == bid && r.Type == type, ct);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfPullRequestArtifactStore.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner is SqliteException sqlite
                && (sqlite.SqliteErrorCode == SqliteConstraintErrorCode
                    || sqlite.SqliteExtendedErrorCode == SqliteUniqueErrorCode))
            {
                return true;
            }
            inner = inner.InnerException;
        }
        return false;
    }

    private static void DetachInserts(ThroneDbContext ctx)
    {
        foreach (var entry in ctx.ChangeTracker.Entries<PullRequestArtifactRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
