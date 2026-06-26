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
        if (result.Launch is { } launch)
        {
            response.Launch = ToLaunchDto(launch);
        }
        return response;
    }

    private static TerminalLaunchArgs ToLaunchDto(TerminalLaunchRecord launch) => new()
    {
        Mode = ParseRunMode(launch.Mode),
        Vendor = launch.Vendor,
        Model = launch.Model,
        Effort = launch.Effort is { } effort ? ParseEffort(effort) : null,
    };

    public static TerminalLaunchInput ToLaunchInput(RunIntentTerminalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new TerminalLaunchInput(
            Vendor: request.Vendor,
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

    public static string? ToReviewBindingId(RunIntentTerminalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.IsNullOrWhiteSpace(request.Review_binding_id)
            ? null
            : request.Review_binding_id;
    }

    public static IReadOnlyList<string>? ToSelectedSkillIds(RunIntentTerminalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Selected_skill_ids?.ToArray();
    }

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

    private static TerminalRunMode ParseRunMode(string mode) => mode switch
    {
        TerminalRunModes.Work => TerminalRunMode.Work,
        TerminalRunModes.Interview => TerminalRunMode.Interview,
        TerminalRunModes.Review => TerminalRunMode.Review,
        TerminalRunModes.Dream => TerminalRunMode.Dream,
        TerminalRunModes.Free => TerminalRunMode.Free,
        _ => throw new InvalidOperationException($"Unknown terminal run mode '{mode}'."),
    };

    private static TerminalReasoningEffort ParseEffort(string effort) => effort switch
    {
        TerminalAgentCatalog.EffortLow => TerminalReasoningEffort.Low,
        TerminalAgentCatalog.EffortMedium => TerminalReasoningEffort.Medium,
        TerminalAgentCatalog.EffortHigh => TerminalReasoningEffort.High,
        TerminalAgentCatalog.EffortXhigh => TerminalReasoningEffort.Xhigh,
        _ => throw new InvalidOperationException($"Unknown reasoning effort '{effort}'."),
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
