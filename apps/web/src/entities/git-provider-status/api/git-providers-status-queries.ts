import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type { SettingsComponents } from "@/shared/api";

import type { GitProvidersStatus } from "../model/types";
import {
  fetchGitProvidersStatus,
  setGitLabHost
} from "./git-providers-status-api";

type GitLabHostSettings =
  SettingsComponents["schemas"]["GitLabHostSettingsDto"];

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

export function useSetGitLabHost(): UseMutationResult<
  GitLabHostSettings,
  Error,
  string
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (host: string) => setGitLabHost(host),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: gitProvidersStatusQueryKeys.current()
      });
    }
  });
}
