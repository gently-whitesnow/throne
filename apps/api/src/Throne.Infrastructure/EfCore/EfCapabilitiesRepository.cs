using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="ICapabilitiesRepository"/> backing the singleton aggregate. One row
/// keyed by <see cref="Capabilities.SingletonId"/>; <c>SaveAsync</c> upserts in place
/// (insert-or-update) and the row's <c>current_version</c> doubles as an optimistic
/// concurrency token so a concurrent operator save loses cleanly.
/// </summary>
internal sealed class EfCapabilitiesRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), ICapabilitiesRepository
{
    public Task<Capabilities?> GetAsync(CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<CapabilitiesRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == Capabilities.SingletonId, c);
            return row is null ? null : CapabilitiesRowMapper.ToDomain(row);
        }, ct);

    public async Task SaveAsync(Capabilities capabilities, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var ctx = RequireWriteContext(nameof(SaveAsync));

        var existing = await ctx.Set<CapabilitiesRow>()
            .FirstOrDefaultAsync(r => r.Id == Capabilities.SingletonId, ct);
        if (existing is null)
        {
            ctx.Set<CapabilitiesRow>().Add(CapabilitiesRowMapper.ToRow(capabilities));
        }
        else
        {
            existing.CurrentVersion = capabilities.CurrentVersion;
            existing.UpdatedAt = capabilities.UpdatedAt;
            existing.Selections = new Dictionary<string, string>(capabilities.Selections, StringComparer.Ordinal);
        }
        await ctx.SaveChangesAsync(ct);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfCapabilitiesRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
