import { httpGet, promptPartPatchesEndpoints } from "@/shared/api";

import type { PromptPartPatchDetail } from "../model/types";

export function getPromptPartPatch(
  patchId: string,
  signal?: AbortSignal
): Promise<PromptPartPatchDetail> {
  return httpGet<PromptPartPatchDetail>(
    promptPartPatchesEndpoints.getPromptPartPatch(patchId),
    signal
  );
}
