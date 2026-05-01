using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Application.Instructions;

public sealed class GetInstructionBundleHandler(IInstructionRepository repository)
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

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        return new InstructionBundle(query.Mode, intentId, ordered, missing);
    }
}
