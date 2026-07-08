import {
  cardAttachmentsEndpoints,
  httpDelete,
  httpGet,
  httpPost
} from "@/shared/api";

import type { AttachCardRequest, CardAttachment } from "../model/types";

export function listIntentCardAttachments(
  intentId: string,
  signal?: AbortSignal
): Promise<CardAttachment[]> {
  return httpGet<CardAttachment[]>(
    cardAttachmentsEndpoints.listIntentCardAttachments(intentId),
    signal
  );
}

export function attachIntentCard(
  intentId: string,
  body: AttachCardRequest,
  signal?: AbortSignal
): Promise<CardAttachment> {
  return httpPost<CardAttachment>(
    cardAttachmentsEndpoints.attachIntentCardAttachment(intentId),
    body,
    signal
  );
}

export function detachIntentCard(
  intentId: string,
  attachmentId: string,
  signal?: AbortSignal
): Promise<void> {
  return httpDelete(
    cardAttachmentsEndpoints.detachIntentCardAttachment(intentId, attachmentId),
    signal
  );
}

export function refreshIntentCard(
  intentId: string,
  attachmentId: string,
  signal?: AbortSignal
): Promise<CardAttachment> {
  return httpPost<CardAttachment>(
    cardAttachmentsEndpoints.refreshIntentCardAttachment(
      intentId,
      attachmentId
    ),
    null,
    signal
  );
}
