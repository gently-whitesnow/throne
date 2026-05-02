using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Instructions;

public sealed class GetInstructionBundleHandler(
    IInstructionRepository repository,
    SystemInstructionCatalog systemCatalog,
    IIntentRepository intents,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<InstructionBundle> HandleAsync(GetInstructionBundleQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<string> requiredKinds;
        try
        {
            requiredKinds = InstructionBundleModeNames.RequiredKindsFor(query.Mode);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Unknown instruction bundle mode.",
                new Dictionary<string, object?>
                {
                    ["mode"] = query.Mode,
                    ["allowed_modes"] = InstructionBundleModeNames.All.ToArray(),
                });
        }

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        if (intentId is not null)
        {
            var status = query.Mode switch
            {
                InstructionBundleModeNames.Interview => IntentStatusNames.Interview,
                InstructionBundleModeNames.Work or InstructionBundleModeNames.NewProject or InstructionBundleModeNames.Fix
                    => IntentStatusNames.Work,
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
                    ct).ConfigureAwait(false);
            }
        }

        var systemEntries = new List<InstructionWithText>(requiredKinds.Count);
        var missing = new List<string>();
        foreach (var kind in requiredKinds)
        {
            if (systemCatalog.TryGetText(kind, out var text))
            {
                systemEntries.Add(new InstructionWithText(
                    Scope: InstructionScopeNames.System,
                    Kind: kind,
                    InstructionId: SystemInstructionCatalog.SyntheticInstructionId(kind),
                    CurrentVersion: 1,
                    Text: text));
            }
            else
            {
                missing.Add(kind);
            }
        }

        var userInstructions = await repository
            .GetUserInstructionsByKindsAsync(MvpUser.Id, requiredKinds, ct)
            .ConfigureAwait(false);

        var kindOrder = requiredKinds
            .Select((kind, index) => new { kind, index })
            .ToDictionary(x => x.kind, x => x.index, StringComparer.Ordinal);

        var userEntries = userInstructions
            .OrderBy(i => kindOrder.TryGetValue(i.Kind, out var idx) ? idx : int.MaxValue)
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
}
