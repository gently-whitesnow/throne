using Throne.Api.PromptPartPatches;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class PromptPartPatchRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        PromptPartPatchProposed proposed => new RealtimeEventEnvelope(
            RealtimeEventNames.PromptPartPatchProposed, PromptPartPatchDtoMapper.ToDto(proposed.Patch)),
        PromptPartPatchApplied applied => new RealtimeEventEnvelope(
            RealtimeEventNames.PromptPartPatchApplied, PromptPartPatchDtoMapper.ToDto(applied.Patch)),
        PromptPartPatchRejected rejected => new RealtimeEventEnvelope(
            RealtimeEventNames.PromptPartPatchRejected, PromptPartPatchDtoMapper.ToDto(rejected.Patch)),
        PromptPartPatchSuperseded superseded => new RealtimeEventEnvelope(
            RealtimeEventNames.PromptPartPatchSuperseded, PromptPartPatchDtoMapper.ToDto(superseded.Patch)),
        _ => null,
    };
}
