import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type { TerminalAgentVendor, TerminalSettings } from "../model/types";
import {
  fetchTerminalSettings,
  setDefaultTerminalVendor
} from "./terminal-settings-api";

export const terminalSettingsQueryKeys = {
  all: ["terminal-settings"] as const,
  current: () => [...terminalSettingsQueryKeys.all, "current"] as const
};

export function useTerminalSettingsQuery(): UseQueryResult<TerminalSettings> {
  return useQuery({
    queryKey: terminalSettingsQueryKeys.current(),
    queryFn: ({ signal }) => fetchTerminalSettings(signal),
    staleTime: 30_000
  });
}

export function useSetDefaultTerminalVendor(): UseMutationResult<
  TerminalSettings,
  Error,
  TerminalAgentVendor
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (vendor) => setDefaultTerminalVendor(vendor),
    onSuccess: (updated) => {
      queryClient.setQueryData<TerminalSettings | undefined>(
        terminalSettingsQueryKeys.current(),
        updated
      );
    }
  });
}
