using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// LoggerMessage source-generator host for <see cref="GhReviewThreadsReader"/>.
/// </summary>
internal static partial class GhReviewThreadsReaderLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "gh graphql review-thread read failed for {Owner}/{Repo} PR #{Number}.")]
    public static partial void ReadFailed(ILogger logger, string owner, string repo, int number, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "gh graphql returned {Cap}+ review threads for {Owner}/{Repo} PR #{Number}; resolution state may be incomplete.")]
    public static partial void ThreadsCapped(ILogger logger, int cap, string owner, string repo, int number);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "gh graphql review thread {ThreadId} for {Owner}/{Repo} PR #{Number} has {Cap}+ comments; some may not be linked.")]
    public static partial void CommentsCapped(ILogger logger, string threadId, string owner, string repo, int number, int cap);
}
