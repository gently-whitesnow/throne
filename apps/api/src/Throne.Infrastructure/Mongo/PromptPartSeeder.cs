using Microsoft.Extensions.Hosting;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Idempotent startup service (ADR-0036). On every start it SEEDs/reconciles <c>system</c>
/// prompt parts with the skill manifest: creates missing parts as v1, writes a new version on
/// text drift, and reconciles mode-roles. Runs after <see cref="MongoIndexInitializer"/>.
/// </summary>
internal sealed class PromptPartSeeder(
    ISkillManifestProvider manifestProvider,
    IPromptPartRepository parts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);

    /// <summary>Runs the system-part seed/reconcile pass once. Exposed for integration tests.</summary>
    internal async Task RunAsync(CancellationToken ct)
    {
        var manifest = manifestProvider.Current;
        await SeedSystemPartsAsync(manifest, ct);
    }

    private async Task SeedSystemPartsAsync(SkillManifest manifest, CancellationToken ct)
    {
        foreach (var entry in manifest.SystemInstructions)
        {
            var existing = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, entry.Kind, ct);
            if (existing is null)
            {
                await CreateAsync(SystemPromptPartId(entry.Kind), PromptPartScopeNames.System, entry.Kind, entry.Text, manifest, ct);
                continue;
            }

            await ReconcileSystemPartAsync(existing, entry, manifest, ct);
        }
    }

    private async Task ReconcileSystemPartAsync(
        PromptPart existing, SystemInstructionEntry entry, SkillManifest manifest, CancellationToken ct)
    {
        if (!string.Equals(existing.Text, entry.Text, StringComparison.Ordinal))
        {
            var now = clock.GetUtcNow();
            await unitOfWork.ExecuteAsync(
                inner => parts.ReplaceTextAsync(
                    existing.Id,
                    existing.CurrentVersion,
                    existing.Text,
                    entry.Text,
                    TextVersionAuthor.System,
                    now,
                    inner),
                ct);
        }

        var desiredRoles = PromptPartManifestRoles.MandatoryRolesFor(
            PromptPartScopeNames.System, entry.Kind, manifest);
        if (!RolesEqual(existing.ModeRoles, desiredRoles))
        {
            var now = clock.GetUtcNow();
            await unitOfWork.ExecuteAsync(
                inner => parts.SetModeRolesAsync(existing.Id, desiredRoles, now, inner),
                ct);
        }
    }

    private async Task CreateAsync(
        PromptPartId id,
        string scope,
        string key,
        string text,
        SkillManifest manifest,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var modeRoles = PromptPartManifestRoles.MandatoryRolesFor(scope, key, manifest);
        var part = PromptPart.Create(id, scope, key, text, description: null, modeRoles, now);
        var initialVersion = TextVersion.CreateSnapshot(
            id: Guid.NewGuid().ToString("N"),
            ownerKind: TextVersionOwnerKind.PromptPart,
            ownerId: part.Id.Value,
            snapshot: part.Text,
            changedAt: now,
            changedBy: TextVersionAuthor.System);
        await unitOfWork.ExecuteAsync(inner => parts.CreateAsync(part, initialVersion, inner), ct);
    }

    private static PromptPartId SystemPromptPartId(string key) => new($"system:{key}");

    private static bool RolesEqual(IReadOnlyList<PromptPartModeRole> a, IReadOnlyList<PromptPartModeRole> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        var ordered = a.OrderBy(r => r.Mode, StringComparer.Ordinal).ToList();
        var desired = b.OrderBy(r => r.Mode, StringComparer.Ordinal).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!string.Equals(ordered[i].Mode, desired[i].Mode, StringComparison.Ordinal)
                || !string.Equals(ordered[i].Role, desired[i].Role, StringComparison.Ordinal)
                || ordered[i].Order != desired[i].Order)
            {
                return false;
            }
        }
        return true;
    }
}
