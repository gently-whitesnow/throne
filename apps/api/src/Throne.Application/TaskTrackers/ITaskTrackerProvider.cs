namespace Throne.Application.TaskTrackers;

/// <summary>
/// Port for an external task-tracker provider (Kaiten / Jira / Linear / … — ADR-0045).
/// This slice ships the provider-neutral core: the port carries only the stable identity used by
/// <see cref="ITaskTrackerProviderRegistry"/> and the catalog surface. Concrete adapters
/// (the Kaiten HTTP client and beyond) extend this port with the read/write behaviour they need.
/// </summary>
public interface ITaskTrackerProvider
{
    /// <summary>
    /// Stable provider key this implementation handles. Open wire key (ADR-0046): the value set is
    /// delivered by the catalog and validated against the registry, not by a generated enum.
    /// </summary>
    string TrackerKey { get; }

    /// <summary>Human-readable label for settings/catalog rows.</summary>
    string DisplayName { get; }
}
