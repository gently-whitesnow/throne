using Throne.Application.Git;

namespace Throne.Infrastructure.Git;

internal sealed class WorkspaceDirectoryProbe : IWorkspaceDirectoryProbe
{
    public bool Exists(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        return Directory.Exists(absolutePath);
    }
}
