import { httpGet, instructionPatchesEndpoints } from "@/shared/api";

import type { InstructionPatchDetail } from "../model/types";

export function getInstructionPatch(
  patchId: string,
  signal?: AbortSignal
): Promise<InstructionPatchDetail> {
  return httpGet<InstructionPatchDetail>(
    instructionPatchesEndpoints.getInstructionPatch(patchId),
    signal
  );
}
