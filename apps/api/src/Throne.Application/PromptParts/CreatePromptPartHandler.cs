using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Application.PromptParts;

public sealed record CreatePromptPartCommand(
    string Key,
    string Text,
    string? Description,
    IReadOnlyList<PromptPartModeRole> ModeRoles);

/// <summary>
/// Creates an operator-authored optional prompt part (always <c>user</c> scope in this
/// slice — system optional parts are not introduced, ADR-0035). A duplicate key yields 409.
/// </summary>
public sealed class CreatePromptPartHandler(
    IPromptPartRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<PromptPart> HandleAsync(CreatePromptPartCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Text);

        // Deterministic conflict check (the unique (scope,key) index is a backstop that may
        // still be materializing on a fresh database). Single-user local surface — no race.
        var existing = await repository.ListAsync(ct);
        if (existing.Any(p => string.Equals(p.Key, command.Key, StringComparison.Ordinal)))
        {
            throw new ApiException(
                ErrorCodes.PromptPartAlreadyExists,
                $"Prompt part with key '{command.Key}' already exists.",
                new Dictionary<string, object?> { ["key"] = command.Key });
        }

        var now = clock.GetUtcNow();
        PromptPart part;
        try
        {
            part = PromptPart.Create(
                id: PromptPartId.New(),
                scope: PromptPartScopeNames.User,
                key: command.Key,
                text: command.Text,
                description: command.Description,
                modeRoles: command.ModeRoles ?? [],
                now: now);
        }
        catch (ArgumentException ex)
        {
            throw PromptPartErrors.Validation(ex);
        }

        var initialVersion = TextVersion.CreateSnapshot(
            id: Guid.NewGuid().ToString("N"),
            ownerKind: TextVersionOwnerKind.PromptPart,
            ownerId: part.Id.Value,
            snapshot: part.Text,
            changedAt: now,
            changedBy: TextVersionAuthor.User);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.CreateAsync(part, initialVersion, inner),
            ct);

        return outcome switch
        {
            CreatePromptPartOutcome.Created created => created.Part,
            CreatePromptPartOutcome.KeyConflict => throw new ApiException(
                ErrorCodes.PromptPartAlreadyExists,
                $"Prompt part with key '{command.Key}' already exists.",
                new Dictionary<string, object?> { ["key"] = command.Key }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
