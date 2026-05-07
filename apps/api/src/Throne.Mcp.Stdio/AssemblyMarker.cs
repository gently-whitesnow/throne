namespace Throne.Mcp.Stdio;

/// <summary>
/// Stable assembly anchor for architecture tests. Throne.Mcp.Stdio is an
/// intentionally thin STDIO→HTTP MCP proxy with no domain dependencies (ADR-0009);
/// the architecture rules use this marker to verify that.
/// </summary>
public sealed class AssemblyMarker;
