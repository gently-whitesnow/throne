import { capabilitiesEndpoints, httpGet, httpPut } from "@/shared/api";

import type { Capability, CapabilityName } from "../model/types";

export function fetchCapabilities(signal?: AbortSignal): Promise<Capability[]> {
  return httpGet<Capability[]>(
    capabilitiesEndpoints.listCapabilities(),
    signal
  );
}

export function setCapabilityEnabled(
  name: CapabilityName,
  enabled: boolean,
  signal?: AbortSignal
): Promise<Capability> {
  return httpPut<Capability>(
    capabilitiesEndpoints.setCapabilityEnabled(name),
    { enabled },
    signal
  );
}
