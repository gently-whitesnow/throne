using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public sealed record GetUserInstructionQuery(string Kind);

public sealed record UserInstructionResult(
    string InstructionId,
    string Kind,
    int CurrentVersion,
    string Text);

public sealed class GetUserInstructionHandler(IInstructionRepository repository)
{
    public async Task<UserInstructionResult> HandleAsync(GetUserInstructionQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Kind))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "kind must be a non-empty string.",
                new Dictionary<string, object?> { ["kind"] = query.Kind });
        }

        if (!InstructionKindNames.IsKnown(query.Kind))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown instruction kind: {query.Kind}.",
                new Dictionary<string, object?> { ["kind"] = query.Kind });
        }

        var matches = await repository
            .GetUserInstructionsByKindsAsync(MvpUser.Id, [query.Kind], ct)
            .ConfigureAwait(false);

        if (matches.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"User instruction with kind '{query.Kind}' not found for current user.",
                new Dictionary<string, object?>
                {
                    ["kind"] = query.Kind,
                    ["user_id"] = MvpUser.Id,
                });
        }

        var instruction = matches[0];

        return new UserInstructionResult(
            InstructionId: instruction.Id.Value,
            Kind: instruction.Kind,
            CurrentVersion: instruction.CurrentVersion,
            Text: instruction.Text);
    }
}
