import {
  httpDelete,
  httpPost,
  intentsEndpoints,
  type IntentsComponents
} from "@/shared/api";

type CreateIntentLinkRequest =
  IntentsComponents["schemas"]["CreateIntentLinkRequest"];
type IntentLinkDto = IntentsComponents["schemas"]["IntentLinkDto"];

export function createIntentLink(
  intentId: string,
  body: CreateIntentLinkRequest,
  signal?: AbortSignal
): Promise<IntentLinkDto> {
  return httpPost<IntentLinkDto>(
    intentsEndpoints.createIntentLink(intentId),
    body,
    signal
  );
}

export function deleteIntentLink(
  intentId: string,
  toId: string,
  type: string,
  signal?: AbortSignal
): Promise<void> {
  return httpDelete(
    intentsEndpoints.deleteIntentLink(intentId, toId, type),
    signal
  );
}
