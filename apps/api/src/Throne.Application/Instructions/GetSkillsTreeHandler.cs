using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public sealed class GetSkillsTreeHandler(
    ISkillManifestProvider manifestProvider,
    IInstructionRepository repository)
{
    public async Task<SkillsTree> HandleAsync(GetSkillsTreeQuery _, CancellationToken ct)
    {
        var manifest = manifestProvider.Current;
        var bundlesByMode = manifest.Bundles.ToDictionary(b => b.Mode, StringComparer.Ordinal);

        var allUserKinds = manifest.Bundles
            .SelectMany(b => b.Includes)
            .Where(i => string.Equals(i.Scope, InstructionScopeNames.User, StringComparison.Ordinal))
            .Select(i => i.Kind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var userInstructions = allUserKinds.Length == 0
            ? Array.Empty<Instruction>()
            : await repository.GetUserInstructionsByKindsAsync(MvpUser.Id, allUserKinds, ct);

        var userByKind = userInstructions
            .GroupBy(i => i.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CreatedAt).First(), StringComparer.Ordinal);

        var skills = new List<SkillNode>(manifest.Skills.Count);
        foreach (var skill in manifest.Skills)
        {
            if (!bundlesByMode.TryGetValue(skill.BundleMode, out var bundle))
            {
                continue;
            }

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

            skills.Add(new SkillNode(
                Name: skill.Name,
                Description: skill.Description,
                LauncherBody: skill.LauncherBody,
                Bundle: new BundleNode(skill.BundleMode, entries)));
        }

        return new SkillsTree(skills);
    }
}
