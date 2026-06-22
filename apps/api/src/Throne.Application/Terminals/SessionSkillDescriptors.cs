namespace Throne.Application.Terminals;

/// <summary>
/// The concrete <see cref="SessionSkillDescriptor"/> definitions, one per operational skill. Each
/// is registered individually into DI and surfaced through <see cref="ISessionSkillCatalog"/>
/// (ADR-0045). The canonical skill content lives under <c>skills/&lt;id&gt;/</c> in the repo; this
/// is its registration + behaviour (default modes, package factory).
/// </summary>
public static class SessionSkillDescriptors
{
    public static readonly SessionSkillDescriptor Intent = new(
        SessionSkillPackageIds.Intent,
        SessionSkillPackageSources.Throne,
        "Intent",
        "Правка Intent.text и создание/линковка дочерних интентов.",
        DefaultModes: [TerminalRunModes.Interview],
        CreatePackage: static _ => new IntentSessionSkillPackage());

    public static readonly SessionSkillDescriptor Review = new(
        SessionSkillPackageIds.Review,
        SessionSkillPackageSources.Throne,
        "Review",
        "Запись PR-артефакта review_recommendation через привязанный репозиторий.",
        DefaultModes: [TerminalRunModes.Review],
        // Review needs a resolved write target — without an attached PR/MR it is not
        // materialisable, so the factory returns null and the registry skips it.
        CreatePackage: static resolution => resolution.ReviewArtifact is null
            ? null
            : new ReviewSessionSkillPackage(resolution.ReviewArtifact));

    public static readonly SessionSkillDescriptor Dream = new(
        SessionSkillPackageIds.Dream,
        SessionSkillPackageSources.Throne,
        "Dream",
        "Dream sources, sessions и PromptPartPatch proposals через CLI.",
        DefaultModes: [TerminalRunModes.Dream],
        CreatePackage: static _ => new DreamSessionSkillPackage());
}
