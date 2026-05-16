namespace Throne.Domain.Instructions;

internal static class InstructionGuards
{
    public static void EnsureCreateInputs(string scope, string? userId, string kind, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(text);
        EnsureKnownScope(scope);
        EnsureKnownKind(kind);
        InstructionScopeRules.EnsureUserIdMatchesScope(scope, userId);
    }

    public static void EnsureValidCurrentVersion(int currentVersion)
    {
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }
    }

    public static void EnsureValidOldTextForReplace(string oldText, string currentText)
    {
        if (oldText.Length == 0 && currentText.Length != 0)
        {
            throw new ArgumentException(
                "old_text may be empty only when current text is empty (initial fill).",
                nameof(oldText));
        }
    }

    private static void EnsureKnownScope(string scope)
    {
        if (!InstructionScopeNames.IsKnown(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), $"Unknown instruction scope: {scope}.");
        }
    }

    private static void EnsureKnownKind(string kind)
    {
        if (!InstructionKindNames.IsKnown(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), $"Unknown instruction kind: {kind}.");
        }
    }
}
