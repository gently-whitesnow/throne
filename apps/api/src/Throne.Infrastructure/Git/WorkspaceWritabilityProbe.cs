using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Probes that <paramref name="path"/> is writable by creating and deleting a
/// throw-away file. Split out of <see cref="WorkspaceRootInitializer"/> so the
/// initializer's per-type cyclomatic budget (CA1502) stays clean.
/// </summary>
internal static class WorkspaceWritabilityProbe
{
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
