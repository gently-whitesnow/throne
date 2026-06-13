using Throne.Application.Errors;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// Read-only convenience for the frontier agent: returns the current text and version of a
/// PromptPart for a given <c>(scope, key)</c> so the agent can ground <c>base_version</c> in
/// <c>propose_prompt_part_patch</c>. A missing part yields a synthetic view with
/// <c>current_version=0</c> and empty text (apply will lazily create it).
/// </summary>
public sealed class GetCurrentPromptPartHandler(UserPromptPartLookup partLookup)
{
    public async Task<CurrentPromptPartView> HandleAsync(string targetScope, string targetKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        if (!PromptPartScopeNames.IsKnown(targetScope))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown target_scope: {targetScope}.",
                new Dictionary<string, object?> { ["field"] = "target_scope" });
        }

        var part = await partLookup.FindAsync(targetScope, targetKey, ct);
        if (part is null)
        {
            return new CurrentPromptPartView(
                PromptPartId: string.Empty,
                Scope: targetScope,
                Key: targetKey,
                Text: string.Empty,
                CurrentVersion: 0,
                UpdatedAt: DateTimeOffset.MinValue);
        }

        return new CurrentPromptPartView(
            part.Id.Value,
            part.Scope,
            part.Key,
            part.Text,
            part.CurrentVersion,
            part.UpdatedAt);
    }
}

public sealed record CurrentPromptPartView(
    string PromptPartId,
    string Scope,
    string Key,
    string Text,
    int CurrentVersion,
    DateTimeOffset UpdatedAt);
