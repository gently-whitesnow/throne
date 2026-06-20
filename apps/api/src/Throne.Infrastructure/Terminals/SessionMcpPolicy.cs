using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class SessionMcpPolicy
{
    public static bool ShouldEnableThroneMcp(string mode) =>
        !string.Equals(mode, TerminalRunModes.Interview, StringComparison.Ordinal);
}
