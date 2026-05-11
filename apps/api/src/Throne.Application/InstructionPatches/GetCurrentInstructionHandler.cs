using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Read-only convenience for the frontier agent: returns the current text and
/// version of the user's instruction for a given kind so the agent can ground
/// <c>base_instruction_version</c> in <c>propose_instruction_patch</c>. Owner
/// scoping is implicit via <see cref="ICurrentUserAccessor"/>.
/// </summary>
public sealed class GetCurrentInstructionHandler(
    IInstructionRepository instructions,
    ICurrentUserAccessor currentUser)
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

        var list = await instructions.GetUserInstructionsByKindsAsync(currentUser.UserId, [targetKind], ct);
        if (list.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"User instruction with kind '{targetKind}' not found for the current user.",
                new Dictionary<string, object?> { ["target_kind"] = targetKind });
        }

        var instruction = list[0];
        return new CurrentInstructionView(
            instruction.Id.Value,
            instruction.Kind,
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
