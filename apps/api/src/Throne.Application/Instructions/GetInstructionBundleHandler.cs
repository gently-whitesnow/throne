using Throne.Application.Errors;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Instructions;

public sealed class GetInstructionBundleHandler(
    IInstructionRepository repository,
    ISkillManifestProvider manifestProvider,
    IIntentRepository intents,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<InstructionBundle> HandleAsync(GetInstructionBundleQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var manifest = manifestProvider.Current;
        var bundle = manifest.Bundles.FirstOrDefault(b => string.Equals(b.Mode, query.Mode, StringComparison.Ordinal))
            ?? throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Unknown instruction bundle mode.",
                new Dictionary<string, object?>
                {
                    ["mode"] = query.Mode,
                    ["allowed_modes"] = manifest.Bundles.Select(b => b.Mode).ToArray(),
                });

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        if (intentId is not null)
        {
            var status = query.Mode switch
            {
                InstructionBundleModeNames.Interview => IntentStatusNames.Interview,
                InstructionBundleModeNames.Work or InstructionBundleModeNames.Fix => IntentStatusNames.Work,
                _ => null,
            };

            if (status is not null)
            {
                var now = clock.GetUtcNow();
                await unitOfWork.ExecuteAsync(
                    inner => intents.SetStatusAsync(
                        new IntentId(intentId),
                        status,
                        appendText: null,
                        IntentTrainingAuthor.System,
                        $"get_instruction_bundle:{query.Mode}",
                        now,
                        inner),
                    ct);
            }
        }

        var systemSlots = bundle.Includes
            .Where(i => string.Equals(i.Scope, InstructionScopeNames.System, StringComparison.Ordinal))
            .ToArray();
        var userSlots = bundle.Includes
            .Where(i => string.Equals(i.Scope, InstructionScopeNames.User, StringComparison.Ordinal))
            .ToArray();

        var systemEntries = new List<InstructionWithText>(systemSlots.Length);
        var missing = new List<string>();
        foreach (var slot in systemSlots)
        {
            var systemEntry = manifest.SystemInstructions
                .FirstOrDefault(s => string.Equals(s.Kind, slot.Kind, StringComparison.Ordinal));
            if (systemEntry is null)
            {
                missing.Add(slot.Kind);
                continue;
            }

            systemEntries.Add(new InstructionWithText(
                Scope: InstructionScopeNames.System,
                Kind: slot.Kind,
                InstructionId: SyntheticSystemInstructionId(slot.Kind),
                CurrentVersion: 1,
                Text: systemEntry.Text));
        }

        var userKinds = userSlots.Select(s => s.Kind).ToArray();
        var userInstructions = userKinds.Length == 0
            ? Array.Empty<Domain.Instructions.Instruction>()
            : await repository.GetUserInstructionsByKindsAsync(MvpUser.Id, userKinds, ct);

        var userKindOrder = userSlots
            .Select((slot, idx) => (slot.Kind, idx))
            .ToDictionary(x => x.Kind, x => x.idx, StringComparer.Ordinal);

        var userEntries = userInstructions
            .OrderBy(i => userKindOrder.TryGetValue(i.Kind, out var idx) ? idx : int.MaxValue)
            .ThenBy(i => i.CreatedAt)
            .Select(i => new InstructionWithText(
                Scope: InstructionScopeNames.User,
                Kind: i.Kind,
                InstructionId: i.Id.Value,
                CurrentVersion: i.CurrentVersion,
                Text: i.Text))
            .ToArray();

        var ordered = systemEntries.Concat(userEntries).ToArray();

        return new InstructionBundle(query.Mode, intentId, ordered, missing);
    }

    public static string SyntheticSystemInstructionId(string kind) => $"system:{kind}";
}
