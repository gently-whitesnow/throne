import { httpPost, promptPartsEndpoints } from "@/shared/api";

import type { CreatePromptPartRequest, PromptPart } from "../model/types";

export function createPromptPart(
  body: CreatePromptPartRequest,
  signal?: AbortSignal
): Promise<PromptPart> {
  return httpPost<PromptPart>(
    promptPartsEndpoints.createPromptPart(),
    body,
    signal
  );
}
