using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

public sealed record ProposePromptPartPatchCommand(
    string TargetScope,
    string TargetKey,
    string PatchText,
    string Rationale,
    int BaseVersion,
    string? IdempotencyKey = null,
    string Operation = PromptPartPatchOperationNames.ReplaceText,
    IReadOnlyList<PromptPartModeRole>? ModeRoles = null);

internal static class IdempotencyKeyValidator
{
    public const int MaxLength = 64;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw PromptPartPatchExceptions.ValidationFailed(
                "idempotency_key",
                $"idempotency_key must be ≤{MaxLength} characters.");
        }
        return trimmed;
    }
}

/// <summary>
/// Frontier-agent surface for creating a fresh <see cref="PromptPartPatch"/> in
/// status <c>proposed</c>. Validates the target and the base_version (must match the
/// current PromptPart version at create time; the apply path re-checks before persisting).
/// </summary>
public sealed class ProposePromptPartPatchHandler(
    IPromptPartPatchRepository patches,
    UserPromptPartLookup partLookup,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<PromptPartPatch> HandleAsync(ProposePromptPartPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ProposePromptPartPatchValidator.ValidateCommand(command);

        var idempotencyKey = IdempotencyKeyValidator.Normalize(command.IdempotencyKey);

        // Idempotency check runs BEFORE version validation: a retry that arrives after the
        // original patch was applied must still resolve to the original patch instead of a
        // misleading 409 needs_rebase.
        if (idempotencyKey is not null)
        {
            var existing = await patches.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null)
            {
                return existing;
            }
        }

        var targetPart = await partLookup.FindAsync(command.TargetScope, command.TargetKey, ct);
        var currentVersion = targetPart?.CurrentVersion ?? 0;
        if (currentVersion != command.BaseVersion)
        {
            throw new ApiException(
                ErrorCodes.PromptPartPatchNeedsRebase,
                "base_version does not match PromptPart.current_version.",
                new Dictionary<string, object?>
                {
                    ["target_scope"] = command.TargetScope,
                    ["target_key"] = command.TargetKey,
                    ["base_version"] = command.BaseVersion,
                    ["current_version"] = currentVersion,
                });
        }

        var patch = ProposePromptPartPatchFactory.Create(command, clock.GetUtcNow());

        var outcome = await unitOfWork.ExecuteOutsideTransactionAsync(
            inner => patches.CreateAsync(patch, idempotencyKey, inner),
            ct);
        return outcome.Patch;
    }
}

internal static class ProposePromptPartPatchValidator
{
    public static void ValidateCommand(ProposePromptPartPatchCommand command)
    {
        ValidateTarget(command);
        ValidateOperation(command);
        if (command.ModeRoles is not null)
        {
            ValidateModeRoles(command.ModeRoles);
        }
    }

    private static void ValidateTarget(ProposePromptPartPatchCommand command)
    {
        if (!PromptPartScopeNames.IsKnown(command.TargetScope))
        {
            throw PromptPartPatchExceptions.UnknownTargetScope(command.TargetScope);
        }
        if (!string.Equals(command.TargetScope, PromptPartScopeNames.User, StringComparison.Ordinal))
        {
            throw PromptPartPatchExceptions.UnpatchableTargetScope(command.TargetScope);
        }
        if (string.IsNullOrWhiteSpace(command.TargetKey))
        {
            throw PromptPartPatchExceptions.ValidationFailed("target_key", "target_key must not be empty.");
        }
    }

    private static void ValidateOperation(ProposePromptPartPatchCommand command)
    {
        if (!PromptPartPatchOperationNames.IsKnown(command.Operation))
        {
            throw PromptPartPatchExceptions.ValidationFailed(
                "operation",
                $"Unknown operation: {command.Operation}.");
        }
        ValidateTextPayload(command);
        ValidateRolePayload(command);
    }

    private static void ValidateTextPayload(ProposePromptPartPatchCommand command)
    {
        var needsText = command.Operation == PromptPartPatchOperationNames.ReplaceText
            || command.Operation == PromptPartPatchOperationNames.Create;
        if (needsText && string.IsNullOrWhiteSpace(command.PatchText))
        {
            throw PromptPartPatchExceptions.ValidationFailed("patch_text", "patch_text must not be empty.");
        }
    }

    private static void ValidateRolePayload(ProposePromptPartPatchCommand command)
    {
        var needsRoles = command.Operation == PromptPartPatchOperationNames.Create
            || command.Operation == PromptPartPatchOperationNames.SetRoles;
        if (needsRoles && command.ModeRoles is null)
        {
            throw PromptPartPatchExceptions.ValidationFailed(
                "mode_roles",
                "mode_roles must be provided for create and set_roles operations.");
        }
    }

    private static void ValidateModeRoles(IReadOnlyList<PromptPartModeRole> modeRoles)
    {
        try
        {
            PromptPart.ValidateModeRoles(modeRoles);
        }
        catch (ArgumentException ex)
        {
            throw PromptPartPatchExceptions.ValidationFailed("mode_roles", ex.Message);
        }
    }
}

internal static class ProposePromptPartPatchFactory
{
    public static PromptPartPatch Create(
        ProposePromptPartPatchCommand command,
        DateTimeOffset now)
    {
        try
        {
            return PromptPartPatch.Create(
                id: Guid.NewGuid().ToString("N"),
                targetScope: command.TargetScope,
                targetKey: command.TargetKey,
                operation: command.Operation,
                patchText: command.PatchText,
                modeRoles: command.ModeRoles,
                rationale: command.Rationale ?? string.Empty,
                baseVersion: command.BaseVersion,
                now: now);
        }
        catch (ArgumentException ex)
        {
            throw PromptPartPatchExceptions.ValidationFailed(
                ex.ParamName ?? "patch",
                ex.Message);
        }
    }
}
