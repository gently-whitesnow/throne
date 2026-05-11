import type { InstructionPatch } from "@/entities/instruction-patch";
import { httpPost, instructionPatchesEndpoints } from "@/shared/api";

export interface ApplyInstructionPatchInput {
  patchId: string;
  finalText?: string;
}

/**
 * Apply a patch (verbatim or with operator edit). When `finalText` is omitted
 * the server uses `patch_text` and the resulting status is `applied`; passing
 * a divergent value flips status to `applied_edited` and stores the user's
 * text in `applied_text`.
 */
export function applyInstructionPatch(
  input: ApplyInstructionPatchInput,
  signal?: AbortSignal
): Promise<InstructionPatch> {
  const body =
    input.finalText !== undefined ? { final_text: input.finalText } : {};
  return httpPost<InstructionPatch>(
    instructionPatchesEndpoints.applyInstructionPatch(input.patchId),
    body,
    signal
  );
}
