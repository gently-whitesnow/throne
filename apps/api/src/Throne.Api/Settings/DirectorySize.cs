namespace Throne.Api.Settings;

/// <summary>
/// Cheap recursive size aggregator extracted from <see cref="WorkspaceSizeProbe"/>.
/// Uses <see cref="DirectoryInfo.EnumerateFiles(string, SearchOption)"/> directly
/// so we walk in-process without spawning <c>du</c> — Throne already runs cross-
/// platform and the workspace tree stays small in slice 1 (a clone per intent).
/// </summary>
internal static class DirectorySize
{
    public static long Compute(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var di = new DirectoryInfo(root);
        long total = 0;
        // EnumerateFiles + AllDirectories pushes file enumeration onto the BCL,
        // which already skips unreadable subtrees instead of throwing.
        foreach (var file in di.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            try
            {
                total += file.Length;
            }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
            {
                // File may disappear mid-walk (e.g. concurrent clone replacing it);
                // skip without aborting the whole pass.
            }
        }
        return total;
    }
}
