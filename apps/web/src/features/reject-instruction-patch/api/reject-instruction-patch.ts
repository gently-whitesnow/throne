import type { InstructionPatch } from "@/entities/instruction-patch";
import { httpPost, instructionPatchesEndpoints } from "@/shared/api";

export interface RejectInstructionPatchInput {
  patchId: string;
  comment: string;
}

/**
 * Reject a patch with a mandatory comment (≥10 chars after trimming on the
 * server side). The comment is stored in patch state and surfaces in the next
 * frontier round so the same proposal is not re-emitted.
 */
export function rejectInstructionPatch(
  input: RejectInstructionPatchInput,
  signal?: AbortSignal
): Promise<InstructionPatch> {
  return httpPost<InstructionPatch>(
    instructionPatchesEndpoints.rejectInstructionPatch(input.patchId),
    { comment: input.comment },
    signal
  );
}
