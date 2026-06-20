namespace Throne.Application.Terminals;

public abstract record SessionSkillPackage(string Id, string Source);

public sealed record ReviewArtifactSessionSkillPackage(ReviewArtifactWriteTarget Target)
    : SessionSkillPackage(SessionSkillPackageIds.ReviewArtifact, SessionSkillPackageSources.Throne);

public sealed record IntentOperationsSessionSkillPackage(string IntentId)
    : SessionSkillPackage(SessionSkillPackageIds.IntentOperations, SessionSkillPackageSources.Throne);

public static class SessionSkillPackageIds
{
    public const string ReviewArtifact = "throne-review-artifact";
    public const string IntentOperations = "throne-intent-ops";
}

public static class SessionSkillPackageSources
{
    public const string Throne = "throne";
}
