import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { GitProvidersStatus } from "../model/types";
import { fetchGitProvidersStatus } from "./git-providers-status-api";

export const gitProvidersStatusQueryKeys = {
  all: ["git-providers-status"] as const,
  current: () => [...gitProvidersStatusQueryKeys.all, "current"] as const
};

export function useGitProvidersStatusQuery(): UseQueryResult<GitProvidersStatus> {
  return useQuery({
    queryKey: gitProvidersStatusQueryKeys.current(),
    queryFn: ({ signal }) => fetchGitProvidersStatus(signal)
  });
}
