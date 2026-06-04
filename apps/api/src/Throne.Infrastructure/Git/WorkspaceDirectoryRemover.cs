using Throne.Application.Git;

namespace Throne.Infrastructure.Git;

internal sealed class WorkspaceDirectoryRemover : IWorkspaceDirectoryRemover
{
    public Task RemoveAsync(string absolutePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ct.ThrowIfCancellationRequested();

        if (Directory.Exists(absolutePath))
        {
            Directory.Delete(absolutePath, recursive: true);
        }

        return Task.CompletedTask;
    }
}
