using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

/// <summary>
/// Request to resolve an embedded composition. <paramref name="Contour"/> defaults to
/// <c>embedded</c> because every caller of this resolver is the built-in terminal path (preview
/// and run pre-flight); the MCP standalone path goes through <see cref="GetPromptBundleHandler"/>.
/// </summary>
public sealed record ResolvePromptCompositionQuery(
    string Mode,
    IReadOnlyList<string>? SelectedPartIds,
    string IntentText,
    string Contour = PromptContourNames.Embedded);

/// <summary>One resolved part in an embedded composition: a manifest-projected mandatory
/// instruction or an operator-authored optional part, with its selection state.</summary>
public sealed record EffectivePart(
    string PartId,
    string Key,
    string Scope,
    string Role,
    int Order,
    bool Editable,
    bool Present,
    bool Selected,
    string Text);

/// <summary>Result of <see cref="PromptCompositionResolver"/>: the ordered parts plus the
/// assembled system/user prompt zones for the pre-flight modal (ADR-0036).</summary>
public sealed record PromptComposition(
    string Mode,
    IReadOnlyList<EffectivePart> Parts,
    IReadOnlyList<string> SelectedPartIds,
    string SystemPrompt,
    string UserPrompt);
