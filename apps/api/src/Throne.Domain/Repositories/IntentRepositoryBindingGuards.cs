namespace Throne.Domain.Repositories;

internal static class IntentRepositoryBindingGuards
{
    public static void EnsureTransition(string current, string expectedFrom, string to)
    {
        if (!string.Equals(current, expectedFrom, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Illegal clone_status transition: {current} → {to}; expected from={expectedFrom}.");
        }
    }

    /// <summary>
    /// <c>failed</c> is reachable from either <c>pending</c> (interrupted at restart, T-09) or
    /// <c>cloning</c> (clone crashed) per ADR-0024 §5.
    /// </summary>
    public static void EnsureFailedTransition(string current)
    {
        if (current is CloneStatusNames.Pending or CloneStatusNames.Cloning)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Illegal clone_status transition: {current} → {CloneStatusNames.Failed}; "
            + $"expected from={CloneStatusNames.Pending} or {CloneStatusNames.Cloning}.");
    }
}
