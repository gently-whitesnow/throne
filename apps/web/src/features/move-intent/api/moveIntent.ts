import type { IntentDetail } from "@/entities/intent";
import { httpPost, intentsEndpoints } from "@/shared/api";

export interface MoveIntentArgs {
  intentId: string;
  beforeId: string | null;
  afterId: string | null;
}

export function moveIntent(
  { intentId, beforeId, afterId }: MoveIntentArgs,
  signal?: AbortSignal
): Promise<IntentDetail> {
  return httpPost<IntentDetail>(
    `${intentsEndpoints.getIntent(intentId)}/move`,
    { before_id: beforeId, after_id: afterId },
    signal
  );
}
