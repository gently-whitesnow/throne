using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.PromptParts;

/// <summary>
/// ReplaceText + SetModeRoles for prompt parts. Replace performs a CAS update under the
/// expected version and appends a text-version row; mode-roles is a sessionless write
/// inside the UoW that bumps updated_at (and does NOT bump current_version — mirrors the
/// Mongo behavior).
/// </summary>
internal sealed class EfPromptPartTextEditor(EfSessionAccessor sessions)
{
    public async Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var ctx = RequireContext(nameof(ReplaceTextAsync));
        var wire = id.Value;

        var row = await ctx.Set<PromptPartRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new ReplacePromptPartTextOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new ReplacePromptPartTextOutcome.VersionConflict(row.CurrentVersion);
        }

        var part = PromptPartRowMapper.ToDomain(row);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = part.ReplaceText(oldText, newText, newVersionId, now, changedBy);

        return domainResult switch
        {
            ReplacePromptPartTextResult.MatchNotFound m =>
                new ReplacePromptPartTextOutcome.MatchNotFound(m.QueryPreview),
            ReplacePromptPartTextResult.MatchAmbiguous a =>
                new ReplacePromptPartTextOutcome.MatchAmbiguous(a.MatchesCount, a.MatchLines),
            ReplacePromptPartTextResult.Replaced replaced =>
                await PersistReplaceAsync(ctx, part, expectedVersion, replaced, ct),
            _ => throw new InvalidOperationException(
                $"Unhandled domain result: {domainResult.GetType().Name}"),
        };
    }

    public async Task<PromptPart?> SetModeRolesAsync(
        PromptPartId id,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(modeRoles);
        var ctx = RequireContext(nameof(SetModeRolesAsync));
        var wire = id.Value;

        var row = await ctx.Set<PromptPartRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return null;
        }

        var part = PromptPartRowMapper.ToDomain(row);
        part.SetModeRoles(modeRoles, now);

        var payload = part.ModeRoles.Count == 0
            ? new List<PromptPartModeRolePayload>()
            : [.. part.ModeRoles.Select(PromptPartRowMapper.ToPayload)];
        var newUpdatedAt = part.UpdatedAt;

        await ctx.Set<PromptPartRow>()
            .Where(r => r.Id == wire)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ModeRoles, payload)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);
        return part;
    }

    private static async Task<ReplacePromptPartTextOutcome> PersistReplaceAsync(
        ThroneDbContext ctx,
        PromptPart part,
        int expectedVersion,
        ReplacePromptPartTextResult.Replaced replaced,
        CancellationToken ct)
    {
        var wire = part.Id.Value;
        var newText = part.Text;
        var newVersion = part.CurrentVersion;
        var newUpdatedAt = part.UpdatedAt;

        var affected = await ctx.Set<PromptPartRow>()
            .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Text, newText)
                .SetProperty(r => r.CurrentVersion, newVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);
        if (affected == 0)
        {
            var fresh = await ctx.Set<PromptPartRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new ReplacePromptPartTextOutcome.NotFound()
                : new ReplacePromptPartTextOutcome.VersionConflict(fresh.CurrentVersion);
        }

        ctx.Set<TextVersionRow>().Add(TextVersionRowMapper.ToRow(replaced.Version));
        await ctx.SaveChangesAsync(ct);
        return new ReplacePromptPartTextOutcome.Replaced(part);
    }

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfPromptPartTextEditor.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
