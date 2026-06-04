import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type { Capability, CapabilityName } from "../model/types";
import { fetchCapabilities, setCapabilityEnabled } from "./capabilities-api";

export const capabilitiesQueryKeys = {
  all: ["capabilities"] as const,
  list: () => [...capabilitiesQueryKeys.all, "list"] as const
};

export function useCapabilitiesQuery(): UseQueryResult<Capability[]> {
  return useQuery({
    queryKey: capabilitiesQueryKeys.list(),
    queryFn: ({ signal }) => fetchCapabilities(signal),
    staleTime: 30_000
  });
}

interface ToggleArgs {
  name: CapabilityName;
  enabled: boolean;
}

export function useSetCapabilityEnabled(): UseMutationResult<
  Capability,
  Error,
  ToggleArgs
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ name, enabled }) => setCapabilityEnabled(name, enabled),
    onSuccess: (updated) => {
      queryClient.setQueryData<Capability[] | undefined>(
        capabilitiesQueryKeys.list(),
        (prev) =>
          prev?.map((c) => (c.name === updated.name ? updated : c)) ?? [updated]
      );
    }
  });
}
