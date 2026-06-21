namespace Throne.Application.Terminals;

public interface ISessionSkillCatalog
{
    IReadOnlyList<SessionSkillDescriptor> List();
    SessionSkillDescriptor? Find(string skillId);
}

public sealed class InMemorySessionSkillCatalog : ISessionSkillCatalog
{
    private static readonly IReadOnlyList<string> AllVendors =
    [
        TerminalAgentCatalog.VendorClaude,
        TerminalAgentCatalog.VendorCodex,
        TerminalAgentCatalog.VendorOpencode,
    ];

    private static readonly IReadOnlyList<SessionSkillDescriptor> Descriptors =
    [
        new(
            SessionSkillPackageIds.Intent,
            SessionSkillPackageSources.Throne,
            "Intent",
            "Правка Intent.text и создание/линковка дочерних интентов.",
            AllVendors),
        new(
            SessionSkillPackageIds.Review,
            SessionSkillPackageSources.Throne,
            "Review",
            "Запись PR-артефакта review_recommendation через привязанный репозиторий.",
            AllVendors),
        new(
            SessionSkillPackageIds.Dream,
            SessionSkillPackageSources.Throne,
            "Dream",
            "Dream sources, sessions и PromptPartPatch proposals через CLI.",
            AllVendors),
    ];

    public IReadOnlyList<SessionSkillDescriptor> List() => Descriptors;

    public SessionSkillDescriptor? Find(string skillId) =>
        Descriptors.FirstOrDefault(d => string.Equals(d.Id, skillId, StringComparison.Ordinal));
}

public sealed record SessionSkillDescriptor(
    string Id,
    string Source,
    string Title,
    string Description,
    IReadOnlyList<string> Vendors);
