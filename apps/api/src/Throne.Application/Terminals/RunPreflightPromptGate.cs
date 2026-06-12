using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.PromptParts;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.Terminals;

/// <summary>
/// Pre-spawn gate for the embedded pre-flight payload: validates the operator's part selection
/// against the parts actually available in the mode, then persists the task-zone edit to
/// <c>Intent.text</c> (optimistic concurrency) when requested. Kept out of
/// <see cref="RunPreflightOrchestrator"/> so the orchestrator stays a thin sequencer within the
/// project's type-level complexity budget.
/// </summary>
public sealed class RunPreflightPromptGate(
    PromptCompositionResolver resolver,
    ReplaceIntentTextHandler replaceText)
{
    public async Task ApplyAsync(Intent intent, string mode, TerminalSpawnPrompt prompt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(prompt);

        await ValidateSelectionAsync(intent, mode, prompt.SelectedPartIds, ct);
        await PersistIntentTextAsync(intent, prompt.IntentTextSave, ct);
    }

    private async Task ValidateSelectionAsync(
        Intent intent, string mode, IReadOnlyList<string>? selectedPartIds, CancellationToken ct)
    {
        if (selectedPartIds is null || selectedPartIds.Count == 0)
        {
            return;
        }

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(mode, SelectedPartIds: null, intent.State.Text), ct);
        var available = composition.Parts.Select(p => p.PartId).ToHashSet(StringComparer.Ordinal);
        var unknown = selectedPartIds.Where(id => !available.Contains(id)).ToArray();
        if (unknown.Length == 0)
        {
            return;
        }

        throw new ApiException(
            ErrorCodes.ValidationFailed,
            "selected_part_ids contains parts not available in this mode.",
            new Dictionary<string, object?>
            {
                ["mode"] = mode,
                ["unknown_part_ids"] = unknown,
            });
    }

    private async Task PersistIntentTextAsync(Intent intent, IntentTextSave? save, CancellationToken ct)
    {
        if (save is null)
        {
            return;
        }

        await replaceText.HandleAsync(
            new ReplaceIntentTextCommand(
                intent.Id.Value,
                save.ExpectedVersion,
                save.OldText,
                save.NewText,
                TextVersionAuthor.User),
            ct);
    }
}
