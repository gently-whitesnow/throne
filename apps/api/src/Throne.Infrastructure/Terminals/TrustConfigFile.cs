namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Shared read-modify-write plumbing for the per-vendor trust seeders: read the global config
/// (absent file treated as empty), run the vendor's pure transform, and atomically replace the
/// file only when the transform produced a change. The transform owns all merge / refuse-to-
/// clobber semantics and returns <c>null</c> to signal "no write warranted".
/// </summary>
internal static class TrustConfigFile
{
    public static void Seed(string configPath, Func<string?, string?> transform)
    {
        var existing = File.Exists(configPath) ? File.ReadAllText(configPath) : null;

        var updated = transform(existing);
        if (updated is null)
        {
            return;
        }

        AtomicWrite(configPath, updated);
    }

    // Write to a sibling temp file then atomically replace, so a crash mid-write cannot leave the
    // operator's global config truncated. A concurrently-exiting agent instance writing the same
    // file is an inherent race the CLI already runs against itself (last-writer-wins).
    private static void AtomicWrite(string configPath, string content)
    {
        // The vendor's config dir (e.g. ~/.codex) may not exist yet on a first-ever seed.
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var tempPath = configPath + ".throne-" + Environment.ProcessId + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, configPath, overwrite: true);
    }
}
