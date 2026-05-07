using Throne.Application.Auth;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public sealed class GetBundlesTreeHandler(
    ISkillManifestProvider manifestProvider,
    IInstructionRepository repository,
    ICurrentUserAccessor currentUser)
{
    public async Task<BundlesTree> HandleAsync(GetBundlesTreeQuery _, CancellationToken ct)
    {
        var manifest = manifestProvider.Current;

        var allUserKinds = manifest.Bundles
            .SelectMany(b => b.Includes)
            .Where(i => string.Equals(i.Scope, InstructionScopeNames.User, StringComparison.Ordinal))
            .Select(i => i.Kind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var userInstructions = allUserKinds.Length == 0
            ? Array.Empty<Instruction>()
            : await repository.GetUserInstructionsByKindsAsync(currentUser.UserId, allUserKinds, ct);

        var userByKind = userInstructions
            .GroupBy(i => i.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CreatedAt).First(), StringComparer.Ordinal);

        var bundles = new List<BundleNode>(manifest.Bundles.Count);
        foreach (var bundle in manifest.Bundles)
        {
            var entries = new List<BundleEntryNode>(bundle.Includes.Count);
            foreach (var inc in bundle.Includes)
            {
                if (string.Equals(inc.Scope, InstructionScopeNames.System, StringComparison.Ordinal))
                {
                    var sys = manifest.SystemInstructions
                        .FirstOrDefault(s => string.Equals(s.Kind, inc.Kind, StringComparison.Ordinal));
                    entries.Add(new BundleEntryNode(
                        Scope: InstructionScopeNames.System,
                        Kind: inc.Kind,
                        InstructionId: GetInstructionBundleHandler.SyntheticSystemInstructionId(inc.Kind),
                        CurrentVersion: 1,
                        Text: sys?.Text ?? string.Empty,
                        Editable: false,
                        Present: sys is not null));
                }
                else
                {
                    var present = userByKind.TryGetValue(inc.Kind, out var user);
                    entries.Add(new BundleEntryNode(
                        Scope: InstructionScopeNames.User,
                        Kind: inc.Kind,
                        InstructionId: present ? user!.Id.Value : null,
                        CurrentVersion: present ? user!.CurrentVersion : 0,
                        Text: present ? user!.Text : string.Empty,
                        Editable: true,
                        Present: present));
                }
            }

            bundles.Add(new BundleNode(bundle.Mode, entries));
        }

        return new BundlesTree(bundles);
    }
}
