import type { PromptPartPatch } from "@/entities/prompt-part-patch";
import { httpPost, promptPartPatchesEndpoints } from "@/shared/api";

export interface RejectPromptPartPatchInput {
  patchId: string;
  comment: string;
}

/**
 * Reject a patch with a mandatory comment (≥10 chars after trimming on the
 * server side). The comment is stored in patch state and surfaces in the next
 * frontier round so the same proposal is not re-emitted.
 */
export function rejectPromptPartPatch(
  input: RejectPromptPartPatchInput,
  signal?: AbortSignal
): Promise<PromptPartPatch> {
  return httpPost<PromptPartPatch>(
    promptPartPatchesEndpoints.rejectPromptPartPatch(input.patchId),
    { comment: input.comment },
    signal
  );
}
