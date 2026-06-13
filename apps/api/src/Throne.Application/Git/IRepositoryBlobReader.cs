namespace Throne.Application.Git;

public interface IRepositoryBlobReader
{
    Task<RepositoryFileLineSlice> GetFileLinesAsync(
        string workspacePath,
        string sha,
        string path,
        int fromLine,
        int toLine,
        CancellationToken ct);
}

public sealed record RepositoryFileLine(int Line, string Content);

public sealed record RepositoryFileLineSlice(
    int From,
    int To,
    int TotalLines,
    IReadOnlyList<RepositoryFileLine> Lines);

public sealed class RepositoryBlobReadException(string message) : Exception(message);
