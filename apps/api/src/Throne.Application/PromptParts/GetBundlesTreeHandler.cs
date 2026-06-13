using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

/// <summary>
/// Builds the per-mode bundle tree shown on the operator UI. The manifest's bundles drive the
/// ordered <c>(scope, key)</c> includes; texts are read from the <c>prompt_parts</c> collection.
/// </summary>
public sealed class GetBundlesTreeHandler(
    ISkillManifestProvider manifestProvider,
    IPromptPartRepository repository)
{
    public async Task<BundlesTree> HandleAsync(GetBundlesTreeQuery _, CancellationToken ct)
    {
        var manifest = manifestProvider.Current;
        var byScopeKey = await LoadPartsAsync(manifest, ct);

        var bundles = manifest.Bundles
            .Select(bundle => new BundleNode(
                bundle.Mode,
                bundle.Includes.Select(inc => BuildEntry(inc, byScopeKey)).ToList()))
            .ToList();

        return new BundlesTree(bundles);
    }

    private static BundleEntryNode BuildEntry(
        BundleInclude inc,
        IReadOnlyDictionary<(string Scope, string Key), PromptPart> byScopeKey)
    {
        var present = byScopeKey.TryGetValue((inc.Scope, inc.Kind), out var part);
        var editable = string.Equals(inc.Scope, PromptPartScopeNames.User, StringComparison.Ordinal);
        return new BundleEntryNode(
            Scope: inc.Scope,
            Key: inc.Kind,
            PromptPartId: present ? part!.Id.Value : null,
            CurrentVersion: present ? part!.CurrentVersion : 0,
            Text: present ? part!.Text : string.Empty,
            Editable: editable,
            Present: present);
    }

    private async Task<IReadOnlyDictionary<(string Scope, string Key), PromptPart>> LoadPartsAsync(
        SkillManifest manifest,
        CancellationToken ct)
    {
        var map = new Dictionary<(string Scope, string Key), PromptPart>();
        var seen = new HashSet<(string, string)>();
        foreach (var inc in manifest.Bundles.SelectMany(b => b.Includes))
        {
            if (!seen.Add((inc.Scope, inc.Kind)))
            {
                continue;
            }
            var part = await repository.GetByScopeKeyAsync(inc.Scope, inc.Kind, ct);
            if (part is not null)
            {
                map[(inc.Scope, inc.Kind)] = part;
            }
        }
        return map;
    }
}
