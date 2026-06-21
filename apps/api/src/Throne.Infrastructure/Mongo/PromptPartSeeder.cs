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
/// text drift, reconciles mode-roles, and purges system parts that the manifest no longer
/// declares. Runs after <see cref="MongoIndexInitializer"/>.
/// </summary>
internal sealed class PromptPartSeeder(
    ISkillManifestProvider manifestProvider,
    IPromptPartRepository parts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

        await PurgeOrphanedSystemPartsAsync(manifest, ct);
    }

    // System parts are authored only here, from the manifest. Any system part whose key the
    // manifest no longer declares is an orphan from a manifest edit (e.g. a dropped bundle key):
    // it is no longer composed, but ListPromptParts surfaces all scopes, so leave-it-orphaned
    // would keep it visible to operators. Detach its mode-roles (DeleteAsync refuses a part that
    // still carries roles) and delete it. Idempotent: a second pass finds nothing to purge.
    private async Task PurgeOrphanedSystemPartsAsync(SkillManifest manifest, CancellationToken ct)
    {
        var declared = new HashSet<string>(
            manifest.SystemInstructions.Select(e => e.Kind), StringComparer.Ordinal);

        var systemParts = await parts.ListAsync(PromptPartScopeNames.System, ct);
        foreach (var part in systemParts)
        {
            if (declared.Contains(part.Key))
            {
                continue;
            }

            var now = clock.GetUtcNow();
            await unitOfWork.ExecuteAsync(
                async inner =>
                {
                    if (part.ModeRoles.Count > 0)
                    {
                        await parts.SetModeRolesAsync(part.Id, [], now, inner);
                    }
                    await parts.DeleteAsync(part.Id, inner);
                },
                ct);
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
