namespace Throne.Infrastructure.Git;

/// <summary>
/// Expands a leading <c>~</c> in a configured workspace path to the current
/// user's home directory.
/// </summary>
internal static class WorkspacePathExpansion
{
    public static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (HasHomePrefix(path))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }

    private static bool HasHomePrefix(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
        || path.StartsWith("~\\", StringComparison.Ordinal);
}
