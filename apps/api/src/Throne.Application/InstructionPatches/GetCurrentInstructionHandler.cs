using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Read-only convenience for the frontier agent: returns the current text and
/// version of the user's instruction for a given kind so the agent can ground
/// <c>base_instruction_version</c> in <c>propose_instruction_patch</c>.
///
/// Если записи нет — возвращается синтетический view с
/// <c>current_version=0</c> и пустым текстом, чтобы агент мог сразу предложить
/// первичный патч на пустой инструкции (apply создаст запись лениво).
/// </summary>
public sealed class GetCurrentInstructionHandler(
    IInstructionRepository instructions)
{
    public async Task<CurrentInstructionView> HandleAsync(string targetKind, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        if (!InstructionKindNames.IsKnown(targetKind))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown target_kind: {targetKind}.",
                new Dictionary<string, object?> { ["field"] = "target_kind" });
        }

        var list = await instructions.GetUserInstructionsByKindsAsync([targetKind], ct);
        if (list.Count == 0)
        {
            return new CurrentInstructionView(
                InstructionId: string.Empty,
                Kind: targetKind,
                Text: string.Empty,
                CurrentVersion: 0,
                UpdatedAt: DateTimeOffset.MinValue);
        }

        var instruction = list[0];
        return new CurrentInstructionView(
            instruction.Id.Value,
            instruction.Descriptor.Kind,
            instruction.Text,
            instruction.CurrentVersion,
            instruction.UpdatedAt);
    }
}

public sealed record CurrentInstructionView(
    string InstructionId,
    string Kind,
    string Text,
    int CurrentVersion,
    DateTimeOffset UpdatedAt);
