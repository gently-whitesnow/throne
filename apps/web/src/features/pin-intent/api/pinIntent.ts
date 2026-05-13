import type { IntentDetail } from "@/entities/intent";
import { httpDeleteWithBody, httpPost, intentsEndpoints } from "@/shared/api";

export interface PinIntentArgs {
  intentId: string;
  contextTagId: string;
  beforeId?: string | null;
  afterId?: string | null;
}

export function pinIntent(
  { intentId, contextTagId, beforeId, afterId }: PinIntentArgs,
  signal?: AbortSignal
): Promise<IntentDetail> {
  return httpPost<IntentDetail>(
    intentsEndpoints.pinIntent(intentId),
    {
      context_tag_id: contextTagId,
      before_id: beforeId ?? null,
      after_id: afterId ?? null
    },
    signal
  );
}

export interface UnpinIntentArgs {
  intentId: string;
  contextTagId: string;
}

export function unpinIntent(
  { intentId, contextTagId }: UnpinIntentArgs,
  signal?: AbortSignal
): Promise<IntentDetail> {
  return httpDeleteWithBody<IntentDetail>(
    intentsEndpoints.unpinIntent(intentId),
    { context_tag_id: contextTagId },
    signal
  );
}

export interface MovePinArgs {
  intentId: string;
  contextTagId: string;
  beforeId: string | null;
  afterId: string | null;
}

export function movePin(
  { intentId, contextTagId, beforeId, afterId }: MovePinArgs,
  signal?: AbortSignal
): Promise<IntentDetail> {
  return httpPost<IntentDetail>(
    intentsEndpoints.movePin(intentId),
    {
      context_tag_id: contextTagId,
      before_id: beforeId,
      after_id: afterId
    },
    signal
  );
}
