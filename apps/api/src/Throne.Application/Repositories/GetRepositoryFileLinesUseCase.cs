using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed class GetRepositoryFileLinesUseCase(IRepositoryBlobReader reader)
{
    public async Task<RepositoryFileLineSlice> GetAsync(
        IntentRepositoryBinding binding,
        string sha,
        string path,
        int fromLine,
        int toLine,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        EnsureInputs(sha, path, fromLine, toLine);
        if (binding.State.CloneStatus != CloneStatusNames.Ready)
        {
            throw RepositoryBindingFailures.BindingNotReady(binding);
        }

        try
        {
            return await reader.GetFileLinesAsync(
                binding.WorkspacePath,
                sha,
                path,
                fromLine,
                toLine,
                ct);
        }
        catch (RepositoryBlobReadException ex)
        {
            throw new ApiException(
                ErrorCodes.RepositoryBlobNotFound,
                $"File '{path}' at '{sha}' is unavailable in the local clone.",
                new Dictionary<string, object?>
                {
                    ["sha"] = sha,
                    ["path"] = path,
                    ["reason"] = ex.Message,
                });
        }
    }

    private static void EnsureInputs(string sha, string path, int fromLine, int toLine)
    {
        if (string.IsNullOrWhiteSpace(sha) || sha.Length < 7)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Query parameter 'sha' must be a commit SHA.");
        }
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0', StringComparison.Ordinal))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Query parameter 'path' must be a repository path.");
        }
        if (fromLine < 1 || toLine < fromLine)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Line range must satisfy 1 <= from <= to.");
        }
    }
}
