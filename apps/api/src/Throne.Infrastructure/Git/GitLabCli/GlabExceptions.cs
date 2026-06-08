using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabExceptions
{
    public static GitProviderException Timeout(TimeSpan timeout, Exception inner) =>
        new(
            GitProviderErrorKind.NetworkError,
            $"glab command timed out after {timeout.TotalSeconds:0}s.",
            detail: null,
            innerException: inner);

    public static GitProviderException ExecutableMissing(string path, Exception inner) =>
        ToolExecutableMissing("glab", path, inner);

    public static GitProviderException ToolExecutableMissing(string toolName, string path, Exception inner) =>
        new(
            GitProviderErrorKind.CliFailure,
            $"{toolName} executable '{path}' not found.",
            detail: inner.Message,
            innerException: inner);

    public static GitProviderException FromExit(string operation, ProcessRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var stderr = result.StandardError ?? string.Empty;
        var kind = GlabErrorClassifier.Classify(stderr);
        var message = $"glab {operation} failed (exit {result.ExitCode}): {GlabErrorClassifier.OneLine(stderr)}";
        return new GitProviderException(kind, message, detail: stderr);
    }
}
