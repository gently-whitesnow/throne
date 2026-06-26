using System.Formats.Tar;
using System.IO.Compression;

namespace Throne.Api.Cli;

/// <summary>
/// Downloads a release bundle and swaps it over the current install. A running
/// executable cannot be rewritten in place, so the new binary is moved (rename)
/// over the old path — atomic on the same volume and inode-safe on unix, which is
/// why staging happens inside the install's parent directory. Support folders
/// (wwwroot / skills / specs) that ship next to the binary are moved aside and
/// replaced wholesale so the bundle stays internally consistent.
/// </summary>
public static class SelfUpdateInstaller
{
    private const string BinaryName = "throne";

    public static async Task<string> InstallAsync(
        HttpClient http,
        ReleaseAsset asset,
        string installDir,
        string runningBinaryPath,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(asset);

        var installParent = Directory.GetParent(installDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName
            ?? installDir;
        var stamp = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(installParent, $".throne-update-{stamp}");
        var archive = Path.Combine(installParent, $".throne-update-{stamp}{ArchiveSuffix(asset.Name)}");

        try
        {
            await DownloadAsync(http, asset.DownloadUrl, archive, ct);
            Directory.CreateDirectory(staging);
            Extract(archive, staging);

            var newBinary = Path.Combine(staging, BinaryName);
            if (!File.Exists(newBinary))
            {
                throw new InvalidOperationException(
                    $"Release asset '{asset.Name}' did not contain a '{BinaryName}' binary.");
            }

            SwapSupportDirectories(staging, installDir, stamp);

            File.Move(newBinary, runningBinaryPath, overwrite: true);
            MakeExecutable(runningBinaryPath);

            return runningBinaryPath;
        }
        finally
        {
            TryDelete(archive);
            TryDeleteDir(staging);
        }
    }

    private static void SwapSupportDirectories(string staging, string installDir, string stamp)
    {
        foreach (var name in new[] { "wwwroot", "skills", "specs", "bin" })
        {
            var source = Path.Combine(staging, name);
            if (!Directory.Exists(source))
            {
                continue;
            }

            var target = Path.Combine(installDir, name);
            string? aside = null;
            if (Directory.Exists(target))
            {
                aside = $"{target}.old-{stamp}";
                Directory.Move(target, aside);
            }

            Directory.Move(source, target);
            if (aside is not null)
            {
                TryDeleteDir(aside);
            }
        }
    }

    private static async Task DownloadAsync(HttpClient http, string url, string destination, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destination);
        await src.CopyToAsync(dst, ct);
    }

    private static void Extract(string archive, string destination)
    {
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archive, destination, overwriteFiles: true);
            return;
        }

        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: true);
    }

    private static string ArchiveSuffix(string assetName) =>
        assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".tar.gz";

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
