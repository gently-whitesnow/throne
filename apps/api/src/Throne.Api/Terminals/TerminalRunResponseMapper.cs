using Throne.Application.Terminals;
using Throne.Domain.Capabilities;
using Throne.Domain.Repositories;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

internal static class TerminalRunResponseMapper
{
    public static RunIntentTerminalResponse ToDto(RunPreflightResult result)
    {
        var response = new RunIntentTerminalResponse
        {
            Intent_id = result.IntentId,
            Session_name = result.SessionName,
            Session_state = ParseSessionState(result.SessionState),
            Bindings = result.Bindings.Select(ToBindingDto).ToArray(),
        };
        if (result.BlockingBindings.Count > 0)
        {
            response.Blocking_bindings = result.BlockingBindings.ToArray();
        }
        return response;
    }

    public static string ToDomainMode(TerminalRunMode mode) => mode switch
    {
        TerminalRunMode.Work => TerminalRunModes.Work,
        TerminalRunMode.Interview => TerminalRunModes.Interview,
        TerminalRunMode.Dream => TerminalRunModes.Dream,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unknown terminal run mode '{mode}'."),
    };

    private static RunIntentBindingStatusDto ToBindingDto(RunPreflightBindingStatus status) => new()
    {
        Binding_id = status.BindingId,
        Clone_status = ParseCloneStatus(status.CloneStatus),
        Clone_error = status.CloneError,
    };

    private static TerminalSessionState ParseSessionState(string state) => state switch
    {
        TerminalSessionStates.Spawning => TerminalSessionState.Spawning,
        TerminalSessionStates.Running => TerminalSessionState.Running,
        TerminalSessionStates.Blocked => TerminalSessionState.Blocked,
        TerminalSessionStates.Exited => TerminalSessionState.Exited,
        _ => throw new InvalidOperationException($"Unknown session_state '{state}'."),
    };

    private static BindingCloneStatus ParseCloneStatus(string status) => status switch
    {
        CloneStatusNames.Pending => BindingCloneStatus.Pending,
        CloneStatusNames.Cloning => BindingCloneStatus.Cloning,
        CloneStatusNames.Ready => BindingCloneStatus.Ready,
        CloneStatusNames.Failed => BindingCloneStatus.Failed,
        CloneStatusNames.Broken => BindingCloneStatus.Broken,
        _ => throw new InvalidOperationException($"Unknown clone_status '{status}'."),
    };
}
