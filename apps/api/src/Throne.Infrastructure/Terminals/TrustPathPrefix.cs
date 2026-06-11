namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Decides whether a trusted-workspace entry belongs to an intent being cleaned up. The intent
/// directory is the prefix; an entry matches when it is that directory itself or anything nested
/// under it, so a single intent-dir prefix sweeps the current bindings plus any orphan / leftover
/// clones the spec asks us to capture. Comparison is ordinal and slash-anchored (workspace paths
/// are seeded with the host separator) — a sibling like <c>…/intents/abc-extra</c> must not match
/// the prefix <c>…/intents/abc</c>.
/// </summary>
internal static class TrustPathPrefix
{
    public static bool IsUnder(string entryPath, string directoryPrefix)
    {
        var prefix = directoryPrefix.TrimEnd('/', '\\');
        if (prefix.Length == 0)
        {
            return false;
        }

        if (string.Equals(entryPath, prefix, StringComparison.Ordinal))
        {
            return true;
        }

        return entryPath.StartsWith(prefix + "/", StringComparison.Ordinal)
            || entryPath.StartsWith(prefix + "\\", StringComparison.Ordinal);
    }
}
