import { httpGet, httpPost } from "@/shared/api";

export interface RuntimeInstance {
  ephemeral: boolean;
}

export function fetchRuntimeInstance(
  signal?: AbortSignal
): Promise<RuntimeInstance> {
  return httpGet<RuntimeInstance>("/runtime/instance", signal);
}

export function shutdownRuntimeInstance(): Promise<void> {
  return httpPost<null>("/runtime/shutdown", {}).then(() => undefined);
}
