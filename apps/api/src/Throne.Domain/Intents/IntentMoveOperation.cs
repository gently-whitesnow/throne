namespace Throne.Domain.Intents;

public static class IntentMoveOperation
{
    /// <summary>
    /// Reorder intent by assigning a new sort key. Does not bump version nor UpdatedAt —
    /// drag-and-drop is purely positional and must not pollute text-edit history.
    /// </summary>
    public static bool To(Intent intent, string newSortKey)
    {
        ArgumentNullException.ThrowIfNull(intent);
        FractionalIndex.ValidateKey(newSortKey, nameof(newSortKey));
        if (string.Equals(intent.State.SortKey, newSortKey, StringComparison.Ordinal))
        {
            return false;
        }

        intent.State = intent.State with { SortKey = newSortKey };
        return true;
    }
}
