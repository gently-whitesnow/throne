using Microsoft.EntityFrameworkCore;
using Throne.Application.Git;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core mirror of <c>MongoGitLabHostProvider</c>. Lazy-materialises
/// <see cref="GitLabHostNormalizer.Default"/> on first read if the singleton row does not
/// exist, then caches the value in-process. Cache is invalidated on every successful
/// <see cref="SetHostAsync"/>. No <c>IConfiguration</c> / env-var fallback. Operates
/// outside any unit of work — it's a settings provider, not a domain repository.
/// </summary>
internal sealed class EfGitLabHostProvider(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IGitLabHostProvider
{
    private readonly IDbContextFactory<ThroneDbContext> _contextFactory = contextFactory;
    private readonly Lock _gate = new();
    private string? _cached;

    public async Task<string> GetHostAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_cached is { Length: > 0 })
            {
                return _cached;
            }
        }

        // Settings provider can be hit outside any unit of work (e.g. from a glab invocation
        // during the clone worker), so use a one-shot context for the read-or-seed pass.
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<GitLabHostSettingsRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == GitLabHostSettingsRow.SingletonId, ct);

        string host;
        if (row is { Host.Length: > 0 } existing)
        {
            host = existing.Host;
        }
        else
        {
            host = GitLabHostNormalizer.Default;
            await UpsertHostAsync(host, ct);
        }
        lock (_gate)
        {
            _cached = host;
        }
        return host;
    }

    public async Task<string> SetHostAsync(string host, CancellationToken ct)
    {
        var normalized = GitLabHostNormalizer.Validate(host);
        await UpsertHostAsync(normalized, ct);
        lock (_gate)
        {
            _cached = normalized;
        }
        return normalized;
    }

    private async Task UpsertHostAsync(string host, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<GitLabHostSettingsRow>()
            .FirstOrDefaultAsync(r => r.Id == GitLabHostSettingsRow.SingletonId, ct);
        if (row is null)
        {
            context.Set<GitLabHostSettingsRow>().Add(new GitLabHostSettingsRow
            {
                Id = GitLabHostSettingsRow.SingletonId,
                Host = host,
            });
        }
        else
        {
            row.Host = host;
        }
        await context.SaveChangesAsync(ct);
    }
}
