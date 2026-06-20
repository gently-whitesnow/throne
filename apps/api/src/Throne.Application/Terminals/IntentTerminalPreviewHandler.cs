using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

public sealed record IntentTerminalPreviewQuery(
    string IntentId,
    string Mode,
    IReadOnlyList<string>? SelectedPartIds);

/// <summary>
/// Pre-flight preview result: the resolved composition plus the intent version the modal echoes
/// back as <c>expected_version</c> when it persists a task-zone edit on run.
/// </summary>
public sealed record IntentTerminalPreview(PromptComposition Composition, int IntentVersion);

/// <summary>
/// Pre-flight preview (ADR-0036): reads the intent body for the task zone, appends a minimal block
/// listing any current intent attachments by their workspace-relative path (the bytes are staged on
/// spawn so the embedded agent opens them with a native <c>Read</c>), and resolves the embedded prompt
/// composition for the requested mode. Unsupported modes (e.g. <c>dream</c>) are rejected by
/// <see cref="PromptCompositionResolver"/>.
/// </summary>
public sealed class IntentTerminalPreviewHandler(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments,
    PromptCompositionResolver resolver)
{
    public async Task<IntentTerminalPreview> HandleAsync(IntentTerminalPreviewQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var intent = await intents.GetByIdAsync(new IntentId(query.IntentId), ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        var attachmentList = await attachments.ListByIntentAsync(intent.Id, ct);
        var userPrompt = ComposeUserPrompt(intent.State.Text, attachmentList);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(query.Mode, query.SelectedPartIds, userPrompt),
            ct);
        return new IntentTerminalPreview(composition, intent.State.CurrentVersion);
    }

    private static string ComposeUserPrompt(string intentText, IReadOnlyList<IntentAttachment> attachments)
    {
        var block = TerminalAttachmentsContextRenderer.Render(attachments);
        if (block is null)
        {
            return intentText;
        }
        var trimmed = intentText.TrimEnd('\r', '\n');
        return trimmed.Length == 0 ? block : $"{trimmed}\n\n{block}";
    }
}
