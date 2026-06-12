import { httpGet, promptPartsEndpoints } from "@/shared/api";

import type { PromptPartListItem } from "../model/types";

export function listPromptParts(
  signal?: AbortSignal
): Promise<PromptPartListItem[]> {
  return httpGet<PromptPartListItem[]>(
    promptPartsEndpoints.listPromptParts(),
    signal
  );
}
