namespace Throne.Application.Terminals;

public sealed class SessionSkillPackageRegistry(ISessionSkillCatalog catalog)
{
    public IReadOnlyList<SessionSkillPackage> Resolve(SessionSkillPackageResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var result = new List<SessionSkillPackage>();
        var selected = resolution.SelectedSkillIds.ToHashSet(StringComparer.Ordinal);
        foreach (var descriptor in catalog.List())
        {
            if (!selected.Contains(descriptor.Id))
            {
                continue;
            }

            // The descriptor owns construction (and materialisability — null means skip),
            // so a new skill needs no case here (ADR-0045).
            if (descriptor.CreatePackage(resolution) is { } package)
            {
                result.Add(package);
            }
        }

        return result;
    }
}

public sealed record SessionSkillPackageResolution(
    string IntentId,
    string Vendor,
    IReadOnlyList<string> SelectedSkillIds,
    ReviewArtifactWriteTarget? ReviewArtifact);
