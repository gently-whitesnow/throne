namespace Throne.Application.Terminals;

public static class SessionSkillPackageRegistry
{
    private static readonly IReadOnlyList<string> AllVendors =
    [
        TerminalAgentCatalog.VendorClaude,
        TerminalAgentCatalog.VendorCodex,
        TerminalAgentCatalog.VendorOpencode,
    ];

    private static readonly IReadOnlyList<SessionSkillPackageDescriptor> Descriptors =
    [
        new(
            SessionSkillPackageIds.ReviewArtifact,
            SessionSkillPackageSources.Throne,
            [TerminalRunModes.Review],
            AllVendors),
        new(
            SessionSkillPackageIds.IntentOperations,
            SessionSkillPackageSources.Throne,
            [TerminalRunModes.Interview],
            AllVendors),
    ];

    public static IReadOnlyList<SessionSkillPackage> Resolve(SessionSkillPackageResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var result = new List<SessionSkillPackage>();
        foreach (var descriptor in Descriptors)
        {
            if (!descriptor.Matches(resolution.Mode, resolution.Vendor))
            {
                continue;
            }

            AddPackage(result, descriptor, resolution);
        }

        return result;
    }

    private static void AddPackage(
        List<SessionSkillPackage> result,
        SessionSkillPackageDescriptor descriptor,
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
    string Mode,
    string Vendor,
    ReviewArtifactWriteTarget? ReviewArtifact);

internal sealed record SessionSkillPackageDescriptor(
    string Id,
    string Source,
    IReadOnlyList<string> Modes,
    IReadOnlyList<string> Vendors)
{
    public bool Matches(string mode, string vendor) =>
        Modes.Contains(mode, StringComparer.Ordinal)
        && Vendors.Contains(vendor, StringComparer.Ordinal);
}
