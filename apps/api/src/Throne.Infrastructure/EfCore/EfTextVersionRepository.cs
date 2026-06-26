using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

internal sealed class EfTextVersionRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), ITextVersionRepository
{
    public Task<IReadOnlyList<TextVersion>> ListByOwnerAsync(
        TextVersionOwnerKind ownerKind,
        string ownerId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var ownerWire = ownerKind.ToWire();
        return ReadAsync<IReadOnlyList<TextVersion>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<TextVersionRow>()
                .Where(r => r.OwnerKind == ownerWire && r.OwnerId == ownerId)
                .OrderBy(r => r.Version)
                .ToListAsync(c);
            return rows.Select(TextVersionRowMapper.ToDomain).ToList();
        }, ct);
    }
}
