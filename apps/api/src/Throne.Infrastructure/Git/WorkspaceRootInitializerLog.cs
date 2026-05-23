using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Git;

/// <summary>
/// LoggerMessage source-generator host for <see cref="WorkspaceRootInitializer"/>.
/// Split out so the generated delegate-cache code stays out of the initializer's
/// cyclomatic-complexity budget (CA1502).
/// </summary>
internal static partial class WorkspaceRootInitializerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Workspace root resolved: {Path}.")]
    public static partial void Resolved(ILogger logger, string path);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Workspace root {Path} is not writable.")]
    public static partial void NotWritable(ILogger logger, string path, Exception exception);
}
