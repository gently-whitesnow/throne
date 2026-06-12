import { httpDelete, promptPartsEndpoints } from "@/shared/api";

import type { DeletePromptPartResponse } from "../model/types";

/**
 * Hard-deletes a user prompt part. The endpoint echoes the deleted id; since
 * the caller already holds it, we resolve to it after the request succeeds.
 * Throws HttpError on 404 (already gone) or 409 (part still has roles).
 */
export async function deletePromptPart(
  id: string,
  signal?: AbortSignal
): Promise<DeletePromptPartResponse> {
  await httpDelete(promptPartsEndpoints.deletePromptPart(id), signal);
  return { prompt_part_id: id };
}
