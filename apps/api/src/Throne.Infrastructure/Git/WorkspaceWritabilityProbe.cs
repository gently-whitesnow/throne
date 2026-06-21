using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Probes that <paramref name="path"/> is writable by creating and deleting a
/// throw-away file.
/// </summary>
public static class WorkspaceWritabilityProbe
{
    /// <summary>
    /// Non-throwing variant for the settings/readiness surface: <c>true</c> iff
    /// <paramref name="path"/> exists and a throw-away file can be written and deleted.
    /// Folds a missing directory and any access/IO error into <c>false</c>.
    /// </summary>
    public static bool IsWritable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }
        var probe = Path.Combine(path, $".throne-write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
        finally
        {
            TryDelete(probe);
        }
    }

    public static void Verify(string path, ILogger log)
    {
        var probe = Path.Combine(path, $".throne-write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            WorkspaceRootInitializerLog.NotWritable(log, path, ex);
            throw new InvalidOperationException(
                $"Workspace root '{path}' is not writable. Adjust permissions or change '{WorkspaceOptions.SectionName}:Root'.",
                ex);
        }
        finally
        {
            TryDelete(probe);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Best-effort cleanup — leftover probe files are noise, not a hard failure.
        }
    }
}
