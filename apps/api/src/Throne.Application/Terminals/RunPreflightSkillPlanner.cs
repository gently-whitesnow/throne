using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed class RunPreflightSkillPlanner(
    SessionSkillSelectionService selection,
    SessionSkillPackageRegistry registry,
    IIntentTerminalLaunchStore launches)
{
    public RunPreflightSkillPlan Build(
        string intentId,
        string vendor,
        IReadOnlyList<string>? selectedSkillIds,
        string? selectedBindingId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        var selected = selection.Validate(selectedSkillIds, selectedBindingId, bindings);
        var packages = registry.Resolve(new SessionSkillPackageResolution(
            intentId,
            vendor,
            selected.SelectedSkillIds,
            selected.ReviewArtifact));
        return new RunPreflightSkillPlan(selected.SelectedSkillIds, packages);
    }

    public Task SaveAsync(string intentId, string mode, RunPreflightSkillPlan plan, CancellationToken ct) =>
        launches.SaveSelectedSkillIdsAsync(intentId, mode, plan.SelectedSkillIds, ct);
}

public sealed record RunPreflightSkillPlan(
    IReadOnlyList<string> SelectedSkillIds,
    IReadOnlyList<SessionSkillPackage> Packages);
