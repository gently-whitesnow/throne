import { capabilitiesEndpoints, httpGet, httpPut } from "@/shared/api";

import type { Capability, CapabilityName } from "../model/types";

export function fetchCapabilities(signal?: AbortSignal): Promise<Capability[]> {
  return httpGet<Capability[]>(
    capabilitiesEndpoints.listCapabilities(),
    signal
  );
}

export function setCapabilitySelectedProvider(
  name: CapabilityName,
  selectedProvider: string | null,
  signal?: AbortSignal
): Promise<Capability> {
  return httpPut<Capability>(
    capabilitiesEndpoints.setCapabilitySelectedProvider(name),
    { selected_provider: selectedProvider },
    signal
  );
}
