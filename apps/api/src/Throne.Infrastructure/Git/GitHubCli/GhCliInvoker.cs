using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Builds <see cref="ProcessRunRequest"/>s for the <c>gh</c> CLI and converts
/// non-zero exits into typed <see cref="GitProviderException"/>s. Kept separate
/// from <see cref="GitHubCliProvider"/> so the provider itself stays focused on
/// orchestration and parsing — no per-call duplicated error mapping (DI rule
/// from <c>feedback_static_helpers</c>: this is a stateless helper instantiated
/// via DI, not a static service).
/// </summary>
internal sealed class GhCliInvoker(IProcessLauncher launcher, IOptions<GitHubCliOptions> options)
{
    private readonly GitHubCliOptions _opts = options.Value;

    public string ExecutablePath => _opts.ExecutablePath;

    public int PageSize => _opts.PageSize;

    public int SearchFetchLimit => _opts.SearchFetchLimit;

    public Task<ProcessRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct) =>
        RunAsync(arguments, workingDirectory: null, timeout: _opts.DefaultTimeout, ct);

    public Task<ProcessRunResult> RunCloneAsync(IReadOnlyList<string> arguments, CancellationToken ct) =>
        RunAsync(arguments, workingDirectory: null, timeout: _opts.CloneTimeout, ct);

    public Task<ProcessRunResult> RunInAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken ct) =>
        RunAsync(arguments, workingDirectory, _opts.DefaultTimeout, ct);

    public async Task<ProcessRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            return await launcher.RunAsync(
                new ProcessRunRequest(
                    FileName: _opts.ExecutablePath,
                    Arguments: arguments,
                    WorkingDirectory: workingDirectory,
                    Timeout: timeout),
                ct);
        }
        catch (TimeoutException ex)
        {
            throw GhExceptions.Timeout(timeout, ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw GhExceptions.ExecutableMissing(_opts.ExecutablePath, ex);
        }
    }
}
