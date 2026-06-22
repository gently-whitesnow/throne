using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Ide;

/// <summary>
/// Spawns the operator-selected IDE against either an intent's aggregate workspace folder
/// (<c>{workspace_root}/intents/{intent_id}/</c>) or a single binding's clone. Provider is
/// resolved from the persisted <c>open_in_ide.selected_provider</c>, falling back to
/// «whichever provider is detected» when only one candidate exists.
/// </summary>
public sealed class OpenInIdeService(
    CapabilitiesPersistence capabilities,
    ICapabilityDetectionCache detection,
    IIdeOpenerRegistry openers,
    IIntentRepository intents,
    IIntentRepositoryBindingRepository bindings,
    IWorkspaceRootProvider workspaceRoot)
{
    public async Task<OpenInIdeResult> OpenIntentAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var intent = await intents.GetByIdAsync(new IntentId(intentId), ct)
            ?? throw IdeFailures.IntentNotFound(intentId);

        var opener = await ResolveOpenerAsync(ct);
        var workspacePath = WorkspacePathLayout.ComputeIntentRoot(workspaceRoot.ResolvedRoot, intent.Id);
        await opener.OpenAsync(workspacePath, ct);
        return new OpenInIdeResult(workspacePath, opener.ProviderName);
    }

    public async Task<OpenInIdeResult> OpenBindingAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);

        var binding = await bindings.GetByIdAsync(new BindingId(bindingId), ct)
            ?? throw IdeFailures.BindingNotFound(intentId, bindingId);
        if (binding.IntentId.Value != intentId)
        {
            throw IdeFailures.BindingNotFound(intentId, bindingId);
        }

        if (!string.Equals(binding.State.CloneStatus, CloneStatusNames.Ready, StringComparison.Ordinal))
        {
            throw IdeFailures.WorkspaceNotReady(bindingId, binding.State.CloneStatus);
        }

        var opener = await ResolveOpenerAsync(ct);
        var workspacePath = WorkspacePathLayout.Compute(
            workspaceRoot.ResolvedRoot, binding.IntentId, binding.Coordinate);
        await opener.OpenAsync(workspacePath, ct);
        return new OpenInIdeResult(workspacePath, opener.ProviderName);
    }

    private async Task<IIdeOpener> ResolveOpenerAsync(CancellationToken ct)
    {
        var stored = await capabilities.GetAsync(ct);
        var selected = stored?.GetSelectedProvider(CapabilityNames.OpenInIde);

        if (!string.IsNullOrWhiteSpace(selected))
        {
            var opener = openers.Find(selected)
                ?? throw IdeFailures.ProviderUnavailable(selected, "not registered on this Throne build");
            var probe = await detection.GetAsync(opener.ProviderName, ct);
            if (probe is null || !probe.Detected)
            {
                throw IdeFailures.ProviderUnavailable(opener.ProviderName, probe?.Detail);
            }
            return opener;
        }

        var detected = new List<IIdeOpener>();
        foreach (var candidate in openers.All)
        {
            var probe = await detection.GetAsync(candidate.ProviderName, ct);
            if (probe is { Detected: true })
            {
                detected.Add(candidate);
            }
        }

        return detected.Count switch
        {
            0 => throw IdeFailures.NoProviderAvailable(),
            1 => detected[0],
            _ => throw IdeFailures.AmbiguousProvider(detected.Select(o => o.ProviderName).ToList()),
        };
    }
}
