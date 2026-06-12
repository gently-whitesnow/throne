namespace Throne.Application.PromptParts;

public sealed record ResolvePromptCompositionQuery(
    string Mode,
    IReadOnlyList<string>? SelectedPartIds,
    string IntentText);

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
/// assembled system/user prompt zones for the pre-flight modal (ADR-0035).</summary>
public sealed record PromptComposition(
    string Mode,
    IReadOnlyList<EffectivePart> Parts,
    IReadOnlyList<string> SelectedPartIds,
    string SystemPrompt,
    string UserPrompt);
