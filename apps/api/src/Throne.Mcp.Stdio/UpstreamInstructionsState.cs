using Microsoft.Extensions.Logging;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamInstructionsState(ILogger log)
{
    private string? _current;
    private string? _initial;
    private bool _locked;

    public string? InitialServerInstructions => _initial;

    public event Action? ServerInstructionsDiverged;

    public void Update(string? value)
    {
        if (!_locked)
        {
            _initial = value;
            _locked = true;
        }
        else if (!string.Equals(_initial, value, StringComparison.Ordinal))
        {
            StdioProxyLog.UpstreamInstructionsDiverged(log);
            ServerInstructionsDiverged?.Invoke();
        }
        _current = value;
    }
}
