using Throne.Application.Instructions.Manifest;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

/// <summary>
/// Derives the mandatory per-mode roles a <c>(scope, key)</c> prompt part must carry, from the
/// manifest's <c>bundles[].includes</c>. Shared by the startup seeder and the patch apply path
/// (lazy-create of a user part), so a newly created part lands in the right bundles by
/// construction (ADR-0036).
/// </summary>
public static class PromptPartManifestRoles
{
    public static IReadOnlyList<PromptPartModeRole> MandatoryRolesFor(
        string scope, string key, SkillManifest manifest)
    {
        var roles = new List<PromptPartModeRole>();
        foreach (var bundle in manifest.Bundles)
        {
            for (var i = 0; i < bundle.Includes.Count; i++)
            {
                var inc = bundle.Includes[i];
                if (string.Equals(inc.Scope, scope, StringComparison.Ordinal)
                    && string.Equals(inc.Kind, key, StringComparison.Ordinal))
                {
                    roles.Add(new PromptPartModeRole(bundle.Mode, PromptPartRoleNames.Mandatory, i));
                    break;
                }
            }
        }
        return roles;
    }
}
