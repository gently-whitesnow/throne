import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { HttpError } from "@/shared/api";

import type { PullRequestArtifact } from "../model/types";
import { getPullRequestArtifact } from "./pull-request-artifacts-api";

export const pullRequestArtifactsQueryKeys = {
  all: ["pull-request-artifacts"] as const,
  detail: (bindingId: string, type: string) =>
    [...pullRequestArtifactsQueryKeys.all, "detail", bindingId, type] as const
};

/** A 404 means the artifact has not been produced yet — a normal empty state. */
function notFound(error: unknown): boolean {
  return error instanceof HttpError && error.status === 404;
}

export function usePullRequestArtifactQuery(
  bindingId: string | null,
  type: string,
  enabled: boolean
): UseQueryResult<PullRequestArtifact | null> {
  return useQuery({
    queryKey:
      bindingId !== null
        ? pullRequestArtifactsQueryKeys.detail(bindingId, type)
        : pullRequestArtifactsQueryKeys.all,
    queryFn: async ({ signal }) => {
      if (bindingId === null) {
        throw new Error("usePullRequestArtifactQuery: bindingId is required");
      }
      try {
        return await getPullRequestArtifact(bindingId, type, signal);
      } catch (error) {
        if (notFound(error)) return null;
        throw error;
      }
    },
    enabled: enabled && bindingId !== null,
    retry: (count, error) => !notFound(error) && count < 2,
    staleTime: 60_000
  });
}
