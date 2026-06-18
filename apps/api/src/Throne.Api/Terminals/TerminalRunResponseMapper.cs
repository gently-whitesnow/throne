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

    public static TerminalLaunchInput ToLaunchInput(RunIntentTerminalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new TerminalLaunchInput(
            Vendor: request.Vendor is { } vendor ? ToWireVendor(vendor) : null,
            Model: request.Model,
            Effort: request.Effort is { } effort ? ToWireEffort(effort) : null);
    }

    public static TerminalSpawnPrompt ToSpawnPrompt(RunIntentTerminalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new TerminalSpawnPrompt(
            SystemPrompt: request.System_prompt,
            UserPrompt: request.User_prompt,
            SelectedPartIds: request.Selected_part_ids?.ToArray(),
            IntentTextSave: request.Intent_text_update is { } update
                ? new IntentTextSave(update.Expected_version, update.Old_text, update.New_text)
                : null);
    }

    private static string ToWireVendor(TerminalAgentVendor vendor) => vendor switch
    {
        TerminalAgentVendor.Claude => TerminalAgentCatalog.VendorClaude,
        TerminalAgentVendor.Codex => TerminalAgentCatalog.VendorCodex,
        TerminalAgentVendor.Opencode => TerminalAgentCatalog.VendorOpencode,
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'."),
    };

    private static string ToWireEffort(TerminalReasoningEffort effort) => effort switch
    {
        TerminalReasoningEffort.Low => TerminalAgentCatalog.EffortLow,
        TerminalReasoningEffort.Medium => TerminalAgentCatalog.EffortMedium,
        TerminalReasoningEffort.High => TerminalAgentCatalog.EffortHigh,
        TerminalReasoningEffort.Xhigh => TerminalAgentCatalog.EffortXhigh,
        _ => throw new ArgumentOutOfRangeException(nameof(effort), $"Unknown reasoning effort '{effort}'."),
    };

    public static string ToDomainMode(TerminalRunMode mode) => mode switch
    {
        TerminalRunMode.Work => TerminalRunModes.Work,
        TerminalRunMode.Interview => TerminalRunModes.Interview,
        TerminalRunMode.Review => TerminalRunModes.Review,
        TerminalRunMode.Dream => TerminalRunModes.Dream,
        TerminalRunMode.Free => TerminalRunModes.Free,
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
