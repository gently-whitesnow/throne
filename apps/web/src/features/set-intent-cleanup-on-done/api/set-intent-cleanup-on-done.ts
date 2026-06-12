import type { IntentDetail } from "@/entities/intent";
import { httpPost, intentsEndpoints } from "@/shared/api";

export function setIntentCleanupOnDone(
  intentId: string,
  value: boolean
): Promise<IntentDetail> {
  return httpPost<IntentDetail>(
    intentsEndpoints.setIntentCleanupOnDone(intentId),
    { cleanup_local_state_on_done: value }
  );
}
