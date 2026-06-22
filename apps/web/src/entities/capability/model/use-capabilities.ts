import { useCapabilitiesQuery } from "../api/capabilities-queries";
import { OPEN_IN_IDE, type Capability, type CapabilityName } from "./types";

export interface CapabilitiesState {
  capabilities: readonly Capability[];
  isLoading: boolean;
  error: Error | null;
}

const EMPTY: readonly Capability[] = [];

export function useCapabilities(): CapabilitiesState {
  const query = useCapabilitiesQuery();
  return {
    capabilities: query.data ?? EMPTY,
    isLoading: query.isPending,
    error: query.error instanceof Error ? query.error : null
  };
}

export function selectCapability(
  capabilities: readonly Capability[],
  name: CapabilityName
): Capability | undefined {
  // Currently CapabilityName resolves to a single literal — eslint flags the
  // equality as always-true, so compare via a widened string view.
  return capabilities.find((c) => (c.name as string) === (name as string));
}

/**
 * Effective provider for the `open_in_ide` capability:
 * either the operator-selected one (if its provider is still detected) or the
 * single detected provider when nothing is persisted. Returns null when no
 * provider is usable on the host.
 */
export function selectedIdeProvider(
  capabilities: readonly Capability[]
): string | null {
  const cap = selectCapability(capabilities, OPEN_IN_IDE);
  if (cap === undefined) return null;
  const detected = cap.providers.filter((p) => p.detected);
  if (cap.selected_provider !== null && cap.selected_provider !== undefined) {
    const match = detected.find((p) => p.name === cap.selected_provider);
    if (match !== undefined) return match.name;
  }
  return detected.length === 1 ? detected[0].name : null;
}

export function detectedIdeProviders(
  capabilities: readonly Capability[]
): readonly string[] {
  const cap = selectCapability(capabilities, OPEN_IN_IDE);
  if (cap === undefined) return EMPTY_NAMES;
  return cap.providers.filter((p) => p.detected).map((p) => p.name);
}

const EMPTY_NAMES: readonly string[] = [];

export function useSelectedIdeProvider(): string | null {
  const { capabilities } = useCapabilities();
  return selectedIdeProvider(capabilities);
}

export function useDetectedIdeProviders(): readonly string[] {
  const { capabilities } = useCapabilities();
  return detectedIdeProviders(capabilities);
}
