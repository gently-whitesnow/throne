using Throne.Application.Git;

namespace Throne.Infrastructure.Git;

internal sealed class WorkspaceDirectorySizer : IWorkspaceDirectorySizer
{
    public long Measure(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        if (!Directory.Exists(absolutePath))
        {
            return 0;
        }

        var di = new DirectoryInfo(absolutePath);
        long total = 0;
        // EnumerateFiles + AllDirectories pushes file enumeration onto the BCL, which
        // already skips unreadable subtrees instead of throwing.
        foreach (var file in di.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            try
            {
                total += file.Length;
            }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
            {
                // File may disappear mid-walk (e.g. a concurrent clone replacing it);
                // skip without aborting the whole pass.
            }
        }
        return total;
    }
}
