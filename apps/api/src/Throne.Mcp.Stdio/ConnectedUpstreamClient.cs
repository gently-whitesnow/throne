using ModelContextProtocol.Client;

namespace Throne.Mcp.Stdio;

internal sealed record ConnectedUpstreamClient(McpClient Client, IList<McpClientTool> Tools);
