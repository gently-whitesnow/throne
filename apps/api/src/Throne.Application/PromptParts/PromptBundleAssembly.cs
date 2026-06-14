using Throne.Application.Errors;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

internal static class BundleResolver
{
    /// <summary>
    /// Resolves the bundle for a mode and execution contour (ADR-0034). A contour-specific bundle
    /// wins over a contour-neutral one for the same mode; a neutral bundle serves any contour. The
    /// caller fixes the contour (standalone for MCP <c>get_prompt_bundle</c>, embedded for the
    /// terminal preview) — it is not an operator-facing parameter.
    /// </summary>
    public static BundleDefinition ResolveOrThrow(SkillManifest manifest, string mode, string contour)
    {
        var candidates = manifest.Bundles
            .Where(b => string.Equals(b.Mode, mode, StringComparison.Ordinal))
            .ToArray();

        var resolved =
            candidates.FirstOrDefault(b => string.Equals(b.Contour, contour, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault(b => b.Contour is null);

        return resolved
            ?? throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Unknown prompt bundle mode.",
                new Dictionary<string, object?>
                {
                    ["mode"] = mode,
                    ["contour"] = contour,
                    ["allowed_modes"] = manifest.Bundles.Select(b => b.Mode).Distinct().ToArray(),
                });
    }
}

internal static class BundleModeIntentStatus
{
    public static string? FromMode(string mode) => mode switch
    {
        PromptBundleModeNames.Interview => IntentStatusNames.Interview,
        PromptBundleModeNames.Work => IntentStatusNames.Work,
        _ => null,
    };
}

public sealed class IntentStatusAutoTransition(
    IIntentRepository intents,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task RunAsync(string? intentId, string mode, CancellationToken ct)
    {
        if (intentId is null)
        {
            return;
        }
        var status = BundleModeIntentStatus.FromMode(mode);
        if (status is null)
        {
            return;
        }
        var now = clock.GetUtcNow();
        await unitOfWork.ExecuteAsync(
            inner => intents.SetStatusAsync(
                new IntentId(intentId),
                status,
                appendText: null,
                reason: null,
                IntentTrainingAuthor.System,
                $"get_prompt_bundle:{mode}",
                now,
                inner),
            ct);
    }
}

/// <summary>
/// Loads the ordered prompt parts that compose a bundle mode. The manifest's
/// <c>bundles[].includes</c> drives the ordered <c>(scope, key)</c> list; the texts are read
/// from the <c>prompt_parts</c> collection. A missing part is reported in <c>missing_keys</c>
/// and skipped. The seeder keeps system parts and mandatory mode-roles reconciled with the
/// manifest, so this resolver and <see cref="PromptCompositionResolver"/> read the same source.
/// </summary>
public sealed class PromptBundleResolver(IPromptPartRepository promptParts)
{
    public async Task<(List<PromptBundlePart> Parts, List<string> Missing)> BuildAsync(
        BundleDefinition bundle, CancellationToken ct)
    {
        var entries = new List<PromptBundlePart>();
        var missing = new List<string>();
        foreach (var include in bundle.Includes)
        {
            var part = await promptParts.GetByScopeKeyAsync(include.Scope, include.Kind, ct);
            if (part is null)
            {
                missing.Add(include.Kind);
                continue;
            }
            entries.Add(new PromptBundlePart(
                Scope: part.Scope,
                Key: part.Key,
                PromptPartId: part.Id.Value,
                CurrentVersion: part.CurrentVersion,
                Text: part.Text));
        }
        return (entries, missing);
    }
}
