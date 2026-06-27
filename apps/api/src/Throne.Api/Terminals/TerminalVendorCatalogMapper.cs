using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Terminal.Contracts.Generated;
// Capability-availability gating dropped with the toggle model — vendor selectability
// now depends only on the `in_development` flag (ADR-0032 refactor).

namespace Throne.Api.Terminals;

/// <summary>
/// Projects <see cref="TerminalAgentCatalog"/> into the wire DTO for
/// <c>GET /api/v1/terminal/vendors</c>. Backend stays the single source of truth for vendor
/// metadata; the frontend reads this instead of mirroring the catalog. For descriptors with
/// <see cref="TerminalAgentCatalog.ModelSourceLocal"/> the static
/// <see cref="TerminalVendorDescriptor.Models"/> is empty and the live list is fetched via the
/// matching <see cref="IVendorModelCatalog"/>.
///
/// Every registered descriptor is returned. Each carries a <c>login_status</c> (probed CLI
/// login via <see cref="IAgentVendorLoginProbe"/>) and a <c>selectable</c> flag: the settings
/// vendor cards render all vendors with their status, while the launch dropdowns keep only
/// <c>selectable=true</c>. A vendor is non-selectable when it is <c>in_development</c>
/// (currently <c>opencode</c>) or its <see cref="TerminalVendorDescriptor.RequiredCapability"/>
/// is unavailable (capability toggle off or prerequisite probe undetected) — mirroring the
/// GitLab pattern from ADR-0032 § 8.
/// </summary>
public sealed class TerminalVendorCatalogMapper(
    ITerminalVendorCatalog catalog,
    IEnumerable<IVendorModelCatalog> dynamicCatalogs,
    IEnumerable<IAgentVendorLoginProbe> loginProbes,
    ICapabilityDetectionCache detection)
{
    private readonly Dictionary<string, IVendorModelCatalog> _dynamicCatalogs =
        dynamicCatalogs.ToDictionary(c => c.Vendor, StringComparer.Ordinal);

    private readonly Dictionary<string, IAgentVendorLoginProbe> _loginProbes =
        loginProbes.ToDictionary(p => p.Vendor, StringComparer.Ordinal);

    public async Task<TerminalVendorCatalogResponse> ToDtoAsync(CancellationToken ct)
    {
        var vendors = new List<TerminalVendorMetadataDto>(catalog.Descriptors.Count);
        foreach (var descriptor in catalog.Descriptors)
        {
            vendors.Add(await ToVendorDtoAsync(descriptor, ct));
        }
        return new TerminalVendorCatalogResponse
        {
            Default_vendor = TerminalAgentCatalog.DefaultVendor,
            Vendors = vendors,
            Runtime = await ResolveRuntimeAsync(ct),
        };
    }

    // tmux is the embedded-run prerequisite (probed but never a selectable carrier, ADR-0026 § 1);
    // surfaced here so readiness can flag "agent logged in but Run would still 422".
    private async Task<TerminalRuntimePrerequisitesDto> ResolveRuntimeAsync(CancellationToken ct)
    {
        var tmux = await detection.GetAsync("tmux", ct);
        return new TerminalRuntimePrerequisitesDto
        {
            Tmux = new RuntimePrerequisiteStatusDto
            {
                Detected = tmux?.Detected ?? false,
                Detail = tmux?.Detail,
            },
        };
    }

    private static bool IsSelectable(TerminalVendorDescriptor descriptor) => !descriptor.InDevelopment;

    private async Task<(TerminalVendorLoginStatus Status, string? Detail)> ResolveLoginAsync(
        TerminalVendorDescriptor descriptor, CancellationToken ct)
    {
        if (descriptor.InDevelopment)
        {
            return (TerminalVendorLoginStatus.In_development, "в разработке");
        }
        if (_loginProbes.TryGetValue(descriptor.Vendor, out var probe))
        {
            var result = await probe.ProbeAsync(ct);
            return (ParseLoginStatus(result.Status), result.Detail);
        }
        return (TerminalVendorLoginStatus.Logged_out, null);
    }

    private async Task<TerminalVendorMetadataDto> ToVendorDtoAsync(
        TerminalVendorDescriptor descriptor, CancellationToken ct)
    {
        var models = await ResolveModelsAsync(descriptor, ct);
        var login = await ResolveLoginAsync(descriptor, ct);
        return new TerminalVendorMetadataDto
        {
            Vendor = descriptor.Vendor,
            Label = descriptor.Label,
            Supports_effort = descriptor.SupportsEffort,
            Models = models,
            Default_model = models.Count == 0 ? null : models[0],
            // Efforts wire-format is string[] (см. PR #97 / ADR-обоснование):
            // NSwag не цепляет JsonStringEnumConverter к коллекции enum'ов, поэтому
            // массив проходит сырыми строками из descriptor.Efforts. Default_effort
            // остаётся скалярным enum'ом — на нём конвертер работает.
            Efforts = descriptor.Efforts.ToArray(),
            Default_effort = descriptor.DefaultEffort is { } effort ? ParseEffort(effort) : null,
            Model_source = ParseModelSource(descriptor.ModelSource),
            Login_status = login.Status,
            Login_detail = login.Detail,
            Selectable = IsSelectable(descriptor),
        };
    }

    private static TerminalVendorLoginStatus ParseLoginStatus(AgentVendorLoginStatus status) => status switch
    {
        AgentVendorLoginStatus.Ready => TerminalVendorLoginStatus.Ready,
        AgentVendorLoginStatus.LoggedOut => TerminalVendorLoginStatus.Logged_out,
        AgentVendorLoginStatus.Missing => TerminalVendorLoginStatus.Missing,
        _ => TerminalVendorLoginStatus.Logged_out,
    };

    private async Task<IList<string>> ResolveModelsAsync(
        TerminalVendorDescriptor descriptor, CancellationToken ct)
    {
        if (descriptor.ModelSource == TerminalAgentCatalog.ModelSourceLocal
            && _dynamicCatalogs.TryGetValue(descriptor.Vendor, out var dynamicCatalog))
        {
            var live = await dynamicCatalog.ListModelsAsync(ct);
            return live.ToArray();
        }
        return descriptor.Models.ToArray();
    }

    private static TerminalReasoningEffort ParseEffort(string effort) => effort switch
    {
        TerminalAgentCatalog.EffortLow => TerminalReasoningEffort.Low,
        TerminalAgentCatalog.EffortMedium => TerminalReasoningEffort.Medium,
        TerminalAgentCatalog.EffortHigh => TerminalReasoningEffort.High,
        TerminalAgentCatalog.EffortXhigh => TerminalReasoningEffort.Xhigh,
        _ => throw new InvalidOperationException($"Unknown reasoning effort '{effort}'."),
    };

    private static TerminalModelSource ParseModelSource(string source) => source switch
    {
        TerminalAgentCatalog.ModelSourceStatic => TerminalModelSource.Static,
        TerminalAgentCatalog.ModelSourceLocal => TerminalModelSource.Local,
        _ => throw new InvalidOperationException($"Unknown model source '{source}'."),
    };
}
