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

    public Task RemoveContentsAsync(string directoryPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(directoryPath))
        {
            return Task.CompletedTask;
        }

        var root = new DirectoryInfo(directoryPath);
        foreach (var dir in root.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();
            dir.Delete(recursive: true);
        }
        foreach (var file in root.EnumerateFiles())
        {
            ct.ThrowIfCancellationRequested();
            file.Delete();
        }

        return Task.CompletedTask;
    }
}
