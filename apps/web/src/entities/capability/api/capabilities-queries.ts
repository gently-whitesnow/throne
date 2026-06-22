import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type { Capability, CapabilityName } from "../model/types";
import {
  fetchCapabilities,
  setCapabilitySelectedProvider
} from "./capabilities-api";

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

interface SetSelectedProviderArgs {
  name: CapabilityName;
  selectedProvider: string | null;
}

export function useSetSelectedProvider(): UseMutationResult<
  Capability,
  Error,
  SetSelectedProviderArgs
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ name, selectedProvider }) =>
      setCapabilitySelectedProvider(name, selectedProvider),
    onSuccess: (updated) => {
      queryClient.setQueryData<Capability[] | undefined>(
        capabilitiesQueryKeys.list(),
        (prev) =>
          prev?.map((c) =>
            (c.name as string) === (updated.name as string) ? updated : c
          ) ?? [updated]
      );
    }
  });
}
