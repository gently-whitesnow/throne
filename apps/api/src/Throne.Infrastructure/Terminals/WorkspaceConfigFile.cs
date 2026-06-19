namespace Throne.Infrastructure.Terminals;

internal static class WorkspaceConfigFile
{
    public static async Task MergeAsync(string configPath, Func<string?, string?> transform, CancellationToken ct)
    {
        var existing = File.Exists(configPath)
            ? await File.ReadAllTextAsync(configPath, ct)
            : null;

        var updated = transform(existing);
        if (updated is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await File.WriteAllTextAsync(configPath, EnsureTrailingNewline(updated), ct);
    }

    private static string EnsureTrailingNewline(string value) =>
        value.EndsWith('\n') ? value : value + "\n";
}
