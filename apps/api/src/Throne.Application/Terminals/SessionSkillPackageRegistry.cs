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
            case SessionSkillPackageIds.Review when resolution.ReviewArtifact is not null:
                result.Add(new ReviewSessionSkillPackage(resolution.ReviewArtifact));
                break;
            case SessionSkillPackageIds.Intent:
                result.Add(new IntentSessionSkillPackage());
                break;
            case SessionSkillPackageIds.Dream:
                result.Add(new DreamSessionSkillPackage());
                break;
        }
    }
}

public sealed record SessionSkillPackageResolution(
    string IntentId,
    string Vendor,
    IReadOnlyList<string> SelectedSkillIds,
    ReviewArtifactWriteTarget? ReviewArtifact);
