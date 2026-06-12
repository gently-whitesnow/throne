namespace Throne.Domain.TextVersions;

public enum TextVersionOwnerKind
{
    Intent = 1,

    /// <summary>
    /// Legacy owner-kind retired by ADR-0036. Kept only so the startup migration can drop
    /// stale <c>text_versions</c> rows written under it; no new history is recorded with it.
    /// </summary>
    Instruction = 2,

    PromptPart = 3,
}
