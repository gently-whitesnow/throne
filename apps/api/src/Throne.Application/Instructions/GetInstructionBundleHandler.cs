using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Instructions;

public sealed class GetInstructionBundleHandler(
    IInstructionRepository repository,
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
                    ["allowed_modes"] = new[]
                    {
                        InstructionBundleModeNames.Interview,
                        InstructionBundleModeNames.LightWork,
                        InstructionBundleModeNames.NewProject,
                    },
                });
        }

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        if (intentId is not null)
        {
            var status = query.Mode switch
            {
                InstructionBundleModeNames.Interview => IntentStatusNames.Interview,
                InstructionBundleModeNames.LightWork or InstructionBundleModeNames.NewProject => IntentStatusNames.Work,
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

        var instructions = await repository.GetByKindsAsync(requiredKinds, ct).ConfigureAwait(false);
        var kindOrder = requiredKinds
            .Select((kind, index) => new { kind, index })
            .ToDictionary(x => x.kind, x => x.index, StringComparer.Ordinal);

        var ordered = instructions
            .OrderBy(i => kindOrder[i.Kind])
            .ThenBy(i => i.CreatedAt)
            .Select(i => new InstructionWithText(i.Kind, i.Id.Value, i.CurrentVersion, i.Text))
            .ToArray();

        var presentKinds = ordered.Select(i => i.Kind).ToHashSet(StringComparer.Ordinal);
        var missing = requiredKinds.Where(kind => !presentKinds.Contains(kind)).ToArray();

        return new InstructionBundle(query.Mode, intentId, ordered, missing);
    }
}
