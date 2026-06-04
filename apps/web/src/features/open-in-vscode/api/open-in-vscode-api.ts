import { httpPost, vscodeEndpoints, type VscodeComponents } from "@/shared/api";

type OpenInVscodeResponse = VscodeComponents["schemas"]["OpenInVscodeResponse"];

export function openIntentInVscode(
  intentId: string,
  signal?: AbortSignal
): Promise<OpenInVscodeResponse> {
  return httpPost<OpenInVscodeResponse>(
    vscodeEndpoints.openIntentInVscode(intentId),
    undefined,
    signal
  );
}

export function openBindingInVscode(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<OpenInVscodeResponse> {
  return httpPost<OpenInVscodeResponse>(
    vscodeEndpoints.openBindingInVscode(intentId, bindingId),
    undefined,
    signal
  );
}
