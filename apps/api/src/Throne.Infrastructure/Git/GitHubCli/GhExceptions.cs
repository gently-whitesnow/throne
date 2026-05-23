using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Factory helpers for <see cref="GitProviderException"/>. Centralises the
/// short message + structured kind mapping so both <see cref="GhCliInvoker"/>
/// (transport-level failures) and <see cref="GitHubCliProvider"/> (non-zero
/// exits) speak the same vocabulary.
/// </summary>
internal static class GhExceptions
{
    public static GitProviderException Timeout(TimeSpan timeout, Exception inner) =>
        new(
            GitProviderErrorKind.NetworkError,
            $"gh command timed out after {timeout.TotalSeconds:0}s.",
            detail: null,
            innerException: inner);

    public static GitProviderException ExecutableMissing(string path, Exception inner) =>
        new(
            GitProviderErrorKind.CliFailure,
            $"gh CLI executable '{path}' not found. Install it with 'brew install gh' and re-authenticate.",
            detail: inner.Message,
            innerException: inner);

    public static GitProviderException FromExit(string operation, ProcessRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var stderr = result.StandardError ?? string.Empty;
        var kind = GhErrorClassifier.Classify(stderr);
        var message = $"gh {operation} failed (exit {result.ExitCode}): {GhErrorClassifier.OneLine(stderr)}";
        return new GitProviderException(kind, message, detail: stderr);
    }
}
