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
        var result = first;
        ProcessRunResult? fetch = null;
        ProcessRunResult? second = null;
        if (!first.IsSuccess)
        {
            fetch = await FetchAsync(workspacePath, sha, ct);
            if (fetch.IsSuccess)
            {
                second = await ReadBlobAsync(workspacePath, sha, path, ct);
                result = second;
            }
        }
        if (!result.IsSuccess)
        {
            throw new RepositoryBlobReadException(BuildUnavailableReason(first, fetch, second));
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

    private Task<ProcessRunResult> FetchAsync(
        string workspacePath,
        string sha,
        CancellationToken ct) =>
        launcher.RunAsync(
            new ProcessRunRequest(
                FileName: "git",
                Arguments: ["-C", workspacePath, "fetch", "origin", sha],
                Timeout: FetchTimeout),
            ct);

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

    private static string BuildUnavailableReason(
        ProcessRunResult firstShow,
        ProcessRunResult? fetch,
        ProcessRunResult? secondShow)
    {
        if (fetch is { IsSuccess: false })
        {
            return $"git fetch failed: {FormatFailure(fetch)}; initial git show failed: {FormatFailure(firstShow)}";
        }
        if (secondShow is not null)
        {
            return $"git show failed after fetch: {FormatFailure(secondShow)}; initial git show failed: {FormatFailure(firstShow)}";
        }
        return $"git show failed: {FormatFailure(firstShow)}";
    }

    private static string FormatFailure(ProcessRunResult result)
    {
        var output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        output = string.IsNullOrWhiteSpace(output) ? "no output" : output.Trim();
        return $"exit {result.ExitCode}: {output}";
    }

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
