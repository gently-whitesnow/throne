using Throne.Application.Manifest;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

public sealed class GetPromptBundleHandler(
    ISkillManifestProvider manifestProvider,
    IntentStatusAutoTransition autoTransition,
    PromptBundleResolver bundleResolver)
{
    public async Task<PromptBundle> HandleAsync(GetPromptBundleQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var manifest = manifestProvider.Current;
        // get_prompt_bundle is the MCP standalone path (ADR-0034); the contour is fixed here, not
        // an operator-facing parameter.
        var bundle = BundleResolver.ResolveOrThrow(manifest, query.Mode, PromptContourNames.Standalone);

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        await autoTransition.RunAsync(intentId, query.Mode, ct);

        var (parts, missing) = await bundleResolver.BuildAsync(bundle, ct);
        return new PromptBundle(query.Mode, intentId, parts, missing);
    }
}
