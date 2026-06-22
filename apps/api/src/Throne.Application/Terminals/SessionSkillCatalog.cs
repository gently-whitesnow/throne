namespace Throne.Application.Terminals;

public interface ISessionSkillCatalog
{
    IReadOnlyList<SessionSkillDescriptor> List();
    SessionSkillDescriptor? Find(string skillId);
}

/// <summary>
/// Default <see cref="ISessionSkillCatalog"/>. Indexes the DI-registered descriptors by
/// <see cref="SessionSkillDescriptor.Id"/> while preserving registration order — same
/// port → DI fan-out → registry idiom as git providers and vendors (ADR-0045). Adding a skill is
/// a new descriptor + folder + one DI line; mode-defaults and package construction travel on the
/// descriptor, so no central switch is edited.
/// </summary>
public sealed class SessionSkillCatalog : ISessionSkillCatalog
{
    private readonly IReadOnlyList<SessionSkillDescriptor> _ordered;
    private readonly Dictionary<string, SessionSkillDescriptor> _byId;

    public SessionSkillCatalog(IEnumerable<SessionSkillDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var ordered = new List<SessionSkillDescriptor>();
        var map = new Dictionary<string, SessionSkillDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (!map.TryAdd(descriptor.Id, descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate SessionSkillDescriptor registration for '{descriptor.Id}'.");
            }
            ordered.Add(descriptor);
        }

        _ordered = ordered;
        _byId = map;
    }

    public IReadOnlyList<SessionSkillDescriptor> List() => _ordered;

    public SessionSkillDescriptor? Find(string skillId) =>
        string.IsNullOrEmpty(skillId) ? null : _byId.GetValueOrDefault(skillId);
}

/// <summary>
/// Metadata + behaviour for one operational session skill. <see cref="DefaultModes"/> drives the
/// mode→skill default seeding and <see cref="CreatePackage"/> builds the materialisable package
/// for a given resolution (returning <see langword="null"/> when the skill is not materialisable,
/// e.g. <c>review</c> without an attached PR/MR) — both data-driven off the descriptor so adding a
/// skill needs no edits to the seeds or the package registry (ADR-0045).
/// </summary>
public sealed record SessionSkillDescriptor(
    string Id,
    string Source,
    string Title,
    string Description,
    IReadOnlyList<string> Vendors,
    IReadOnlyList<string> DefaultModes,
    Func<SessionSkillPackageResolution, SessionSkillPackage?> CreatePackage)
{
    public bool IsDefaultFor(string mode) => DefaultModes.Contains(mode, StringComparer.Ordinal);
}
