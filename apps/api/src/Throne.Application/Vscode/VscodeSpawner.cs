using Throne.Application.Ports;

namespace Throne.Application.Vscode;

/// <summary>
/// Thin shell-out around the host `code` CLI, used by <see cref="OpenInVscodeService"/>.
/// </summary>
public sealed class VscodeSpawner(IProcessLauncher launcher)
{
    private const string CodeCli = "code";
    private static readonly TimeSpan SpawnTimeout = TimeSpan.FromSeconds(10);

    public async Task SpawnAsync(string workspacePath, CancellationToken ct)
    {
        var request = new ProcessRunRequest(
            FileName: CodeCli,
            Arguments: [workspacePath],
            Timeout: SpawnTimeout);

        ProcessRunResult result;
        try
        {
            result = await launcher.RunAsync(request, ct);
        }
        catch (TimeoutException ex)
        {
            throw VscodeFailures.SpawnFailed(workspacePath, ex.Message);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Probe was OK at TTL time but the binary disappeared in between.
            // Surface as "not detected" rather than a generic 500.
            throw VscodeFailures.CapabilityUndetected();
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"code exited with {result.ExitCode}"
                : result.StandardError.Trim();
            throw VscodeFailures.SpawnFailed(workspacePath, detail);
        }
    }
}
