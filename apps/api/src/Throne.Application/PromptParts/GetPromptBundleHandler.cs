using Throne.Application.Manifest;

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
        var bundle = BundleResolver.ResolveOrThrow(manifest, query.Mode);

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        await autoTransition.RunAsync(intentId, query.Mode, ct);

        var (parts, missing) = await bundleResolver.BuildAsync(bundle, ct);
        return new PromptBundle(query.Mode, intentId, parts, missing);
    }
}
