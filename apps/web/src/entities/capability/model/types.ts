import type { CapabilitiesComponents } from "@/shared/api";

export type Capability = CapabilitiesComponents["schemas"]["CapabilityDto"];
export type CapabilityName =
  CapabilitiesComponents["schemas"]["CapabilityName"];
export type CapabilityProvider =
  CapabilitiesComponents["schemas"]["CapabilityProviderDto"];

export const OPEN_IN_IDE: CapabilityName = "open_in_ide";
