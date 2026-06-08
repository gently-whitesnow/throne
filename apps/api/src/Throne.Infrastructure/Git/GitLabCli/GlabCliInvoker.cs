using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabCliInvoker(IProcessLauncher launcher, IOptions<GitLabCliOptions> options)
{
    private readonly GitLabCliOptions _opts = options.Value;

    public string GitExecutablePath => _opts.GitExecutablePath;

    public int PageSize => _opts.PageSize;

    public Task<ProcessRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct) =>
        RunAsync(arguments, environment: null, ct);

    public Task<ProcessRunResult> RunCloneAsync(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken ct) =>
        RunAsync(arguments, workingDirectory: null, environment, _opts.CloneTimeout, ct);

    public Task<ProcessRunResult> RunInAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken ct) =>
        RunAsync(arguments, workingDirectory, environment, _opts.DefaultTimeout, ct);

    public async Task<ProcessRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken ct) =>
        await RunAsync(arguments, workingDirectory: null, environment, _opts.DefaultTimeout, ct);

    private async Task<ProcessRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
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
                    Environment: environment,
                    Timeout: timeout),
                ct);
        }
        catch (TimeoutException ex)
        {
            throw GlabExceptions.Timeout(timeout, ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw GlabExceptions.ExecutableMissing(_opts.ExecutablePath, ex);
        }
    }
}
