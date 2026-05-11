using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Instructions;

internal static class BundleResolver
{
    public static BundleDefinition ResolveOrThrow(SkillManifest manifest, string mode) =>
        manifest.Bundles.FirstOrDefault(b => string.Equals(b.Mode, mode, StringComparison.Ordinal))
            ?? throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Unknown instruction bundle mode.",
                new Dictionary<string, object?>
                {
                    ["mode"] = mode,
                    ["allowed_modes"] = manifest.Bundles.Select(b => b.Mode).ToArray(),
                });
}

internal static class BundleModeIntentStatus
{
    public static string? FromMode(string mode) => mode switch
    {
        InstructionBundleModeNames.Interview => IntentStatusNames.Interview,
        InstructionBundleModeNames.Work => IntentStatusNames.Work,
        _ => null,
    };
}

public sealed class IntentStatusAutoTransition(
    IIntentRepository intents,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task RunAsync(string? intentId, string mode, CancellationToken ct)
    {
        if (intentId is null)
        {
            return;
        }
        var status = BundleModeIntentStatus.FromMode(mode);
        if (status is null)
        {
            return;
        }
        var now = clock.GetUtcNow();
        await unitOfWork.ExecuteAsync(
            inner => intents.SetStatusAsync(
                new IntentId(intentId),
                status,
                appendText: null,
                IntentTrainingAuthor.System,
                $"get_instruction_bundle:{mode}",
                now,
                inner),
            ct);
    }
}

internal static class SystemBundleEntries
{
    public static (List<InstructionWithText> Entries, List<string> Missing) Build(
        BundleDefinition bundle, SkillManifest manifest)
    {
        var entries = new List<InstructionWithText>();
        var missing = new List<string>();
        foreach (var slot in bundle.Includes.Where(i => string.Equals(
                     i.Scope, InstructionScopeNames.System, StringComparison.Ordinal)))
        {
            AppendSystemEntry(slot, manifest, entries, missing);
        }
        return (entries, missing);
    }

    private static void AppendSystemEntry(
        BundleInclude slot,
        SkillManifest manifest,
        List<InstructionWithText> entries,
        List<string> missing)
    {
        var sys = manifest.SystemInstructions
            .FirstOrDefault(s => string.Equals(s.Kind, slot.Kind, StringComparison.Ordinal));
        if (sys is null)
        {
            missing.Add(slot.Kind);
            return;
        }
        entries.Add(new InstructionWithText(
            Scope: InstructionScopeNames.System,
            Kind: slot.Kind,
            InstructionId: GetInstructionBundleHandler.SyntheticSystemInstructionId(slot.Kind),
            CurrentVersion: 1,
            Text: sys.Text));
    }
}

public sealed class UserBundleEntries(IInstructionRepository repository, ICurrentUserAccessor currentUser)
{
    public async Task<InstructionWithText[]> BuildAsync(BundleDefinition bundle, CancellationToken ct)
    {
        var userSlots = bundle.Includes
            .Where(i => string.Equals(i.Scope, InstructionScopeNames.User, StringComparison.Ordinal))
            .ToArray();
        if (userSlots.Length == 0)
        {
            return [];
        }

        var userKinds = userSlots.Select(s => s.Kind).ToArray();
        var userInstructions = await repository.GetUserInstructionsByKindsAsync(
            currentUser.UserId, userKinds, ct);

        var orderIndex = userSlots
            .Select((slot, idx) => (slot.Kind, idx))
            .ToDictionary(x => x.Kind, x => x.idx, StringComparer.Ordinal);

        return userInstructions
            .OrderBy(i => orderIndex.TryGetValue(i.Kind, out var idx) ? idx : int.MaxValue)
            .ThenBy(i => i.CreatedAt)
            .Select(i => new InstructionWithText(
                Scope: InstructionScopeNames.User,
                Kind: i.Kind,
                InstructionId: i.Id.Value,
                CurrentVersion: i.CurrentVersion,
                Text: i.Text))
            .ToArray();
    }
}
