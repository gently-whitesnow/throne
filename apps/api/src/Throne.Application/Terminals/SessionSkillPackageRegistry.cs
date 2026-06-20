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
            if (!selected.Contains(descriptor.Id)
                || !descriptor.Vendors.Contains(resolution.Vendor, StringComparer.Ordinal))
            {
                continue;
            }

            AddPackage(result, descriptor, resolution);
        }

        return result;
    }

    private static void AddPackage(
        List<SessionSkillPackage> result,
        SessionSkillDescriptor descriptor,
        SessionSkillPackageResolution resolution)
    {
        switch (descriptor.Id)
        {
            case SessionSkillPackageIds.ReviewArtifact when resolution.ReviewArtifact is not null:
                result.Add(new ReviewArtifactSessionSkillPackage(resolution.ReviewArtifact));
                break;
            case SessionSkillPackageIds.IntentOperations:
                result.Add(new IntentOperationsSessionSkillPackage(resolution.IntentId));
                break;
        }
    }
}

public sealed record SessionSkillPackageResolution(
    string IntentId,
    string Vendor,
    IReadOnlyList<string> SelectedSkillIds,
    ReviewArtifactWriteTarget? ReviewArtifact);
