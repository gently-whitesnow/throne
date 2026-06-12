import { httpGet, promptPartsEndpoints } from "@/shared/api";

import type { PromptPart } from "../model/types";

export function getPromptPart(
  id: string,
  signal?: AbortSignal
): Promise<PromptPart> {
  return httpGet<PromptPart>(promptPartsEndpoints.getPromptPart(id), signal);
}
