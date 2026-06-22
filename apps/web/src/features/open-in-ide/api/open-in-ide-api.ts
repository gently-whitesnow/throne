import { httpPost, ideEndpoints, type IdeComponents } from "@/shared/api";

type OpenInIdeResponse = IdeComponents["schemas"]["OpenInIdeResponse"];

export function openIntentInIde(
  intentId: string,
  signal?: AbortSignal
): Promise<OpenInIdeResponse> {
  return httpPost<OpenInIdeResponse>(
    ideEndpoints.openIntentInIde(intentId),
    undefined,
    signal
  );
}

export function openBindingInIde(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<OpenInIdeResponse> {
  return httpPost<OpenInIdeResponse>(
    ideEndpoints.openBindingInIde(intentId, bindingId),
    undefined,
    signal
  );
}
