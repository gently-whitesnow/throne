using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="ITerminalSettingsStore"/>: persists the operator default vendor as the
/// single row in <c>terminal_settings</c>. A never-written row collapses to the native
/// catalog default so callers never special-case absence.
/// </summary>
internal sealed class EfTerminalSettingsStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions,
    ITerminalVendorCatalog vendors)
    : EfRepositoryBase(contextFactory, sessions), ITerminalSettingsStore
{
    public Task<string> GetDefaultVendorAsync(CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<TerminalSettingsRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == TerminalSettingsRow.SingletonId, c);
            return row is null || !vendors.IsKnownVendor(row.DefaultVendor)
                ? TerminalAgentCatalog.DefaultVendor
                : row.DefaultVendor;
        }, ct);

    public async Task SetDefaultVendorAsync(string vendor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);
        var ctx = RequireWriteContext(nameof(SetDefaultVendorAsync));

        var existing = await ctx.Set<TerminalSettingsRow>()
            .FirstOrDefaultAsync(r => r.Id == TerminalSettingsRow.SingletonId, ct);
        if (existing is null)
        {
            ctx.Set<TerminalSettingsRow>().Add(new TerminalSettingsRow
            {
                Id = TerminalSettingsRow.SingletonId,
                DefaultVendor = vendor,
            });
        }
        else
        {
            existing.DefaultVendor = vendor;
        }
        await ctx.SaveChangesAsync(ct);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfTerminalSettingsStore.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
