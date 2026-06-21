import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type {
  SkillModeDefaults,
  UpdateSkillModeDefaultsRequest
} from "../model/types";
import {
  fetchSkillModeDefaults,
  setSkillModeDefaults
} from "./skill-mode-defaults-api";

export const skillModeDefaultsQueryKeys = {
  all: ["skill-mode-defaults"] as const,
  current: () => [...skillModeDefaultsQueryKeys.all, "current"] as const
};

export function useSkillModeDefaultsQuery(): UseQueryResult<SkillModeDefaults> {
  return useQuery({
    queryKey: skillModeDefaultsQueryKeys.current(),
    queryFn: ({ signal }) => fetchSkillModeDefaults(signal),
    staleTime: 30_000
  });
}

export function useSetSkillModeDefaults(): UseMutationResult<
  SkillModeDefaults,
  Error,
  UpdateSkillModeDefaultsRequest
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request) => setSkillModeDefaults(request),
    onSuccess: (updated) => {
      queryClient.setQueryData<SkillModeDefaults | undefined>(
        skillModeDefaultsQueryKeys.current(),
        updated
      );
    }
  });
}
