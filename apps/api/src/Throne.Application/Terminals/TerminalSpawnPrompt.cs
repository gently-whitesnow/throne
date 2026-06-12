namespace Throne.Application.Terminals;

/// <summary>
/// Upfront context the pre-flight modal hands to the spawn (ADR-0034/0035). The backend takes
/// <see cref="SystemPrompt"/> / <see cref="UserPrompt"/> verbatim — it validates the part
/// selection but never silently recomposes the runtime prompt. Empty prompts boot the agent
/// bare; <see cref="IntentTextSave"/> is applied (with optimistic concurrency) before spawn only
/// when the operator opted to persist a task-zone edit.
/// </summary>
public sealed record TerminalSpawnPrompt(
    string? SystemPrompt,
    string? UserPrompt,
    IReadOnlyList<string>? SelectedPartIds,
    IntentTextSave? IntentTextSave)
{
    /// <summary>Empty context — used by callers that do not drive a pre-flight modal (e.g. tests).</summary>
    public static readonly TerminalSpawnPrompt Empty = new(null, null, null, null);
}

/// <summary>
/// Task-zone edit to persist to <c>Intent.text</c> before spawn. Reuses the user-driven
/// replace-text contract: <see cref="OldText"/> is the full pre-edit body shown in the modal,
/// matched and swapped for <see cref="NewText"/> under <see cref="ExpectedVersion"/> optimistic
/// concurrency. A mismatch aborts the spawn so the agent never starts on a stale edit.
/// </summary>
public sealed record IntentTextSave(int ExpectedVersion, string OldText, string NewText);
