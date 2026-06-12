import {
  httpPost,
  promptPartsEndpoints,
  type SharedComponents
} from "@/shared/api";

import type { PromptPart } from "../model/types";

type ReplaceTextRequest = SharedComponents["schemas"]["ReplaceTextRequest"];

export function replacePromptPartText(
  id: string,
  body: ReplaceTextRequest,
  signal?: AbortSignal
): Promise<PromptPart> {
  return httpPost<PromptPart>(
    promptPartsEndpoints.replacePromptPartText(id),
    body,
    signal
  );
}
