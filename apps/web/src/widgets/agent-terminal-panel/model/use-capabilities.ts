import { useQuery } from "@tanstack/react-query";

import { listCapabilities } from "../api/agent-terminal-api";

import type { CapabilityDto, CapabilityName } from "./types";

const CAPABILITIES_KEY = ["capabilities"] as const;
const EMPTY: CapabilityDto[] = [];

export function useCapabilities() {
  const query = useQuery<CapabilityDto[]>({
    queryKey: CAPABILITIES_KEY,
    queryFn: ({ signal }) => listCapabilities(signal),
    staleTime: 30_000
  });
  return {
    capabilities: query.data ?? EMPTY,
    isLoading: query.isPending,
    error: query.error instanceof Error ? query.error : null
  };
}

export function selectCapability(
  capabilities: readonly CapabilityDto[],
  name: CapabilityName
): CapabilityDto | undefined {
  return capabilities.find((c) => c.name === name);
}

export function isCapabilityEnabled(
  capabilities: readonly CapabilityDto[],
  name: CapabilityName
): boolean {
  const cap = selectCapability(capabilities, name);
  return cap !== undefined && cap.enabled && cap.detected;
}
