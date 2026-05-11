using Throne.Application.Instructions.Manifest;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

internal static class BundleEntryBuilder
{
    public static BundleEntryNode Build(
        BundleInclude inc,
        SkillManifest manifest,
        IReadOnlyDictionary<string, Instruction> userByKind) =>
        string.Equals(inc.Scope, InstructionScopeNames.System, StringComparison.Ordinal)
            ? BuildSystemEntry(inc, manifest)
            : BuildUserEntry(inc, userByKind);

    private static BundleEntryNode BuildSystemEntry(BundleInclude inc, SkillManifest manifest)
    {
        var sys = manifest.SystemInstructions
            .FirstOrDefault(s => string.Equals(s.Kind, inc.Kind, StringComparison.Ordinal));
        return new BundleEntryNode(
            Scope: InstructionScopeNames.System,
            Kind: inc.Kind,
            InstructionId: GetInstructionBundleHandler.SyntheticSystemInstructionId(inc.Kind),
            CurrentVersion: 1,
            Text: sys?.Text ?? string.Empty,
            Editable: false,
            Present: sys is not null);
    }

    private static BundleEntryNode BuildUserEntry(
        BundleInclude inc,
        IReadOnlyDictionary<string, Instruction> userByKind)
    {
        var present = userByKind.TryGetValue(inc.Kind, out var user);
        return new BundleEntryNode(
            Scope: InstructionScopeNames.User,
            Kind: inc.Kind,
            InstructionId: present ? user!.Id.Value : null,
            CurrentVersion: present ? user!.CurrentVersion : 0,
            Text: present ? user!.Text : string.Empty,
            Editable: true,
            Present: present);
    }
}
