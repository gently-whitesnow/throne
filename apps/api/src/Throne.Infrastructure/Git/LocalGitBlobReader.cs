using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git;

internal sealed class LocalGitBlobReader(IProcessLauncher launcher) : IRepositoryBlobReader
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromMinutes(2);

    public async Task<RepositoryFileLineSlice> GetFileLinesAsync(
        string workspacePath,
        string sha,
        string path,
        int fromLine,
        int toLine,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var first = await ReadBlobAsync(workspacePath, sha, path, ct);
        var result = first.IsSuccess ? first : await FetchAndReadAsync(workspacePath, sha, path, ct);
        if (!result.IsSuccess)
        {
            throw new RepositoryBlobReadException("git object or path is unavailable");
        }

        var lines = SplitLines(result.StandardOutput);
        var total = lines.Count;
        var effectiveTo = Math.Min(toLine, total);
        var slice = effectiveTo < fromLine
            ? []
            : Enumerable.Range(fromLine, effectiveTo - fromLine + 1)
                .Select(line => new RepositoryFileLine(line, lines[line - 1]))
                .ToList();

        return new RepositoryFileLineSlice(fromLine, effectiveTo, total, slice);
    }

    private async Task<ProcessRunResult> FetchAndReadAsync(
        string workspacePath,
        string sha,
        string path,
        CancellationToken ct)
    {
        await launcher.RunAsync(
            new ProcessRunRequest(
                FileName: "git",
                Arguments: ["-C", workspacePath, "fetch", "--filter=blob:none", "origin", sha],
                Timeout: FetchTimeout),
            ct);
        return await ReadBlobAsync(workspacePath, sha, path, ct);
    }

    private Task<ProcessRunResult> ReadBlobAsync(
        string workspacePath,
        string sha,
        string path,
        CancellationToken ct) =>
        launcher.RunAsync(
            new ProcessRunRequest(
                FileName: "git",
                Arguments: ["-C", workspacePath, "show", "--no-ext-diff", "--no-textconv", $"{sha}:{path}"],
                Timeout: ReadTimeout),
            ct);

    private static List<string> SplitLines(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0 && normalized.EndsWith('\n'))
        {
            lines.RemoveAt(lines.Count - 1);
        }
        return lines;
    }
}
