using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Bindings;

/// <summary>
/// Write surface for <see cref="IntentRepositoryBinding"/> rows: insert (with duplicate-bind
/// race recovery via the UNIQUE coordinate index), state replace, CAS-style clone claim,
/// and delete. Outcomes carry the row state needed for the post-commit event fan-out.
/// </summary>
internal sealed class EfBindingLifecycle(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public async Task<CreateBindingOutcome> CreateAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(CreateAsync));

        var existing = await FindByCoordinateAsync(ctx, binding.IntentId, binding.Coordinate, ct);
        if (existing is not null)
        {
            return new CreateBindingOutcome.Duplicate(IntentRepositoryBindingRowMapper.ToDomain(existing));
        }

        ctx.Set<IntentRepositoryBindingRow>().Add(IntentRepositoryBindingRowMapper.ToRow(binding));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return new CreateBindingOutcome.Created(binding);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            // Lost the race against a concurrent insert. Re-read the winning row so the caller
            // surfaces a clean 409 instead of bubbling a SQLite constraint error.
            DetachInserts(ctx);
            var loser = await FindByCoordinateAsync(ctx, binding.IntentId, binding.Coordinate, ct);
            if (loser is not null)
            {
                return new CreateBindingOutcome.Duplicate(IntentRepositoryBindingRowMapper.ToDomain(loser));
            }
            throw;
        }
    }

    public async Task<SaveBindingOutcome> SaveAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(SaveAsync));

        var id = binding.Id.Value;
        var defaultBranch = binding.State.DefaultBranch;
        var cloneStatus = binding.State.CloneStatus;
        var cloneError = binding.State.CloneError;
        var pullRequestNumber = binding.State.PullRequestNumber;
        var pullRequestState = binding.State.PullRequestState;
        var reviewCommentsEtag = binding.State.ReviewCommentsEtag;
        var lastSeenReviewCommentAt = binding.State.LastSeenReviewCommentAt;
        var lastSyncedAt = binding.State.LastSyncedAt;
        var updatedAt = binding.State.UpdatedAt;
        var suppressMergeAutoClose = binding.State.SuppressMergeAutoClose;

        var affected = await ctx.Set<IntentRepositoryBindingRow>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.DefaultBranch, defaultBranch)
                .SetProperty(r => r.CloneStatus, cloneStatus)
                .SetProperty(r => r.CloneError, cloneError)
                .SetProperty(r => r.PullRequestNumber, pullRequestNumber)
                .SetProperty(r => r.PullRequestState, pullRequestState)
                .SetProperty(r => r.ReviewCommentsEtag, reviewCommentsEtag)
                .SetProperty(r => r.LastSeenReviewCommentAt, lastSeenReviewCommentAt)
                .SetProperty(r => r.LastSyncedAt, lastSyncedAt)
                .SetProperty(r => r.UpdatedAt, updatedAt)
                .SetProperty(r => r.SuppressMergeAutoClose, suppressMergeAutoClose), ct);

        return affected == 0
            ? new SaveBindingOutcome.NotFound()
            : new SaveBindingOutcome.Saved(binding);
    }

    public async Task<SaveBindingOutcome> ClaimCloningAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(ClaimCloningAsync));

        // CAS: the precondition `clone_status == pending` lives in the WHERE clause so two
        // concurrent workers that both dequeued the same binding can't both flip it to
        // cloning. The loser sees 0 rows affected and skips the clone.
        var id = binding.Id.Value;
        var cloneStatus = binding.State.CloneStatus;
        var cloneError = binding.State.CloneError;
        var updatedAt = binding.State.UpdatedAt;
        var pending = CloneStatusNames.Pending;

        var affected = await ctx.Set<IntentRepositoryBindingRow>()
            .Where(r => r.Id == id && r.CloneStatus == pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CloneStatus, cloneStatus)
                .SetProperty(r => r.CloneError, cloneError)
                .SetProperty(r => r.UpdatedAt, updatedAt), ct);

        return affected == 0
            ? new SaveBindingOutcome.NotFound()
            : new SaveBindingOutcome.Saved(binding);
    }

    public async Task<DeleteBindingOutcome> DeleteAsync(BindingId id, CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(DeleteAsync));

        var wire = id.Value;
        var existing = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (existing is null)
        {
            return new DeleteBindingOutcome.NotFound();
        }

        var affected = await ctx.Set<IntentRepositoryBindingRow>()
            .Where(r => r.Id == wire)
            .ExecuteDeleteAsync(ct);

        return affected == 0
            ? new DeleteBindingOutcome.NotFound()
            : new DeleteBindingOutcome.Deleted(IntentRepositoryBindingRowMapper.ToDomain(existing));
    }

    private static Task<IntentRepositoryBindingRow?> FindByCoordinateAsync(
        ThroneDbContext ctx,
        IntentId intentId,
        RepoCoordinate coordinate,
        CancellationToken ct)
    {
        // match on (intent, provider, owner, repo). Host is implied
        // by RepoCoordinate normalization and only scopes self-hosted GitLab instances.
        var intent = intentId.Value;
        var provider = coordinate.Provider;
        var owner = coordinate.Owner;
        var repo = coordinate.Repo;
        return ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.IntentId == intent
                    && r.Provider == provider
                    && r.Owner == owner
                    && r.Repo == repo,
                ct);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfBindingLifecycle.{method} must run inside IUnitOfWork.ExecuteAsync.");

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
        foreach (var entry in ctx.ChangeTracker.Entries<IntentRepositoryBindingRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
