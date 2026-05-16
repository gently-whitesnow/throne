using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamToolsState(ILogger log)
{
    private Tool[] _tools = [];

    public IReadOnlyList<Tool> CurrentTools => _tools;

    public event Action? ToolsChanged;

    public void Update(Tool[] snapshot)
    {
        var changed = !UpstreamToolSnapshotComparer.ToolsEquivalent(_tools, snapshot);
        _tools = snapshot;
        if (changed)
        {
            StdioProxyLog.UpstreamToolsChanged(log, _tools.Length);
            ToolsChanged?.Invoke();
        }
    }
}
