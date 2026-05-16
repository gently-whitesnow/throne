using Throne.Api.InstructionPatches;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class InstructionPatchRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        InstructionPatchProposed proposed => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchProposed, InstructionPatchDtoMapper.ToDto(proposed.Patch)),
        InstructionPatchApplied applied => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchApplied, InstructionPatchDtoMapper.ToDto(applied.Patch)),
        InstructionPatchRejected rejected => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchRejected, InstructionPatchDtoMapper.ToDto(rejected.Patch)),
        InstructionPatchSuperseded superseded => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchSuperseded, InstructionPatchDtoMapper.ToDto(superseded.Patch)),
        _ => null,
    };
}
