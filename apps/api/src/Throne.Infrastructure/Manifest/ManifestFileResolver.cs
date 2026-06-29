namespace Throne.Infrastructure.Manifest;

/// <summary>
/// Resolves a manifest file shipped next to the binary (Content/Link in the csproj) or
/// living in the repo tree during development. Shared by the system-manifest and
/// user-seed providers so both honour the same lookup order: absolute path, content root,
/// app base dir, then walking up from either.
/// </summary>
internal static class ManifestFileResolver
{
    public static string? ResolveExisting(string path, string contentRoot)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return path;
        }

        var candidates = new[]
        {
            Path.Combine(contentRoot, path),
            Path.Combine(AppContext.BaseDirectory, path),
            FindWalkingUp(AppContext.BaseDirectory, path),
            FindWalkingUp(contentRoot, path),
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindWalkingUp(string startDir, string relative)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
