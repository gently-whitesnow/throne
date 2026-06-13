import type { PromptPartPatch } from "@/entities/prompt-part-patch";
import { httpPost, promptPartPatchesEndpoints } from "@/shared/api";

export interface ApplyPromptPartPatchInput {
  patchId: string;
  finalText?: string;
}

/**
 * Apply a patch (verbatim or with operator edit). When `finalText` is omitted
 * the server uses `patch_text` and the resulting status is `applied`; passing
 * a divergent value flips status to `applied_edited` and stores the user's
 * text in `applied_text`.
 */
export function applyPromptPartPatch(
  input: ApplyPromptPartPatchInput,
  signal?: AbortSignal
): Promise<PromptPartPatch> {
  const body =
    input.finalText !== undefined ? { final_text: input.finalText } : {};
  return httpPost<PromptPartPatch>(
    promptPartPatchesEndpoints.applyPromptPartPatch(input.patchId),
    body,
    signal
  );
}
