using Throne.Application.Errors;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Application orchestrator for the <c>capabilities</c> singleton. Combines:
/// <list type="bullet">
///   <item>Static metadata from <see cref="CapabilityCatalog"/>.</item>
///   <item>Persisted toggle via <see cref="CapabilitiesPersistence"/> — default <c>false</c>
///         when no row was ever written (Slice 2 explicit opt-in, ADR-0026 § 1).</item>
///   <item>TTL-cached probe result from <see cref="ICapabilityDetectionCache"/>.</item>
/// </list>
///
/// The persistence layer never sees descriptor strings — the catalog stays in code so
/// changing copy is a deploy, not a migration. <see cref="ToggleAsync"/> is the only
/// writer; the persistence helper materialises the aggregate when the singleton row is missing.
/// </summary>
public sealed class CapabilitiesService(
    CapabilitiesPersistence persistence,
    ICapabilityDetectionCache detection) : ICapabilityAvailability
{
    public async Task<IReadOnlyList<CapabilityView>> ListAsync(CancellationToken ct)
    {
        var stored = await persistence.GetAsync(ct);
        var views = new List<CapabilityView>(CapabilityCatalog.Descriptors.Count);
        foreach (var descriptor in CapabilityCatalog.Descriptors)
        {
            views.Add(await BuildViewAsync(descriptor, stored, ct));
        }
        return views;
    }

    /// <summary>
    /// True only when the capability is both persisted as enabled AND its live probe reports
    /// detected. Used by launch-time gates (vendor catalog filter, run pre-flight) — a
    /// capability that the operator switched on but whose prerequisite is currently missing
    /// is treated the same as disabled, so the dependent UI/vendor disappears instead of
    /// failing the launch downstream with a confusing 500.
    /// </summary>
    public async Task<bool> IsAvailableAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var stored = await persistence.GetAsync(ct);
        if (stored is null || !stored.IsEnabled(name))
        {
            return false;
        }
        var probe = await detection.GetAsync(name, ct);
        return probe?.Detected ?? false;
    }

    public async Task<CapabilityView> ToggleAsync(string name, bool enabled, CancellationToken ct)
    {
        var descriptor = CapabilityCatalog.FindByName(name)
            ?? throw CapabilityFailures.UnknownCapability(name);

        await persistence.UpsertToggleAsync(descriptor.Name, enabled, ct);
        var refreshed = await persistence.GetAsync(ct);
        return await BuildViewAsync(descriptor, refreshed, ct);
    }

    private async Task<CapabilityView> BuildViewAsync(
        CapabilityDescriptor descriptor,
        CapabilitiesAggregate? stored,
        CancellationToken ct)
    {
        var enabled = stored is not null && stored.IsEnabled(descriptor.Name);
        var probe = await detection.GetAsync(descriptor.Name, ct);
        return new CapabilityView(
            Name: descriptor.Name,
            Title: descriptor.Title,
            Description: descriptor.Description,
            PrerequisiteHint: descriptor.PrerequisiteHint,
            Detected: probe?.Detected ?? false,
            DetectionDetail: probe?.Detail,
            Enabled: enabled);
    }
}

internal static class CapabilityFailures
{
    public static ApiException UnknownCapability(string name) =>
        new(
            ErrorCodes.CapabilityNotFound,
            $"Unknown capability '{name}'.",
            new Dictionary<string, object?> { ["capability"] = name });

    public static ApiException CapabilityDisabled(string name) =>
        new(
            ErrorCodes.CapabilityDisabled,
            $"Capability '{name}' is disabled — enable it in /settings before retrying.",
            new Dictionary<string, object?> { ["capability"] = name });
}
