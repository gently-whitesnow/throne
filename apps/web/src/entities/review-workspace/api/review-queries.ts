import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type {
  PullRequestCommit,
  PullRequestDiff,
  PullRequestHeader,
  ReviewDiffScope
} from "../model/types";
import {
  getReviewDiff,
  getReviewPullRequest,
  listReviewCommits
} from "./review-api";

export const reviewWorkspaceQueryKeys = {
  all: ["review-workspace"] as const,
  diff: (
    intentId: string,
    bindingId: string,
    scope: ReviewDiffScope,
    commitSha: string | null
  ) =>
    [
      ...reviewWorkspaceQueryKeys.all,
      "diff",
      intentId,
      bindingId,
      scope,
      commitSha ?? ""
    ] as const,
  commits: (intentId: string, bindingId: string) =>
    [...reviewWorkspaceQueryKeys.all, "commits", intentId, bindingId] as const,
  pullRequest: (intentId: string, bindingId: string) =>
    [
      ...reviewWorkspaceQueryKeys.all,
      "pull-request",
      intentId,
      bindingId
    ] as const
};

export function useReviewDiffQuery(
  intentId: string,
  bindingId: string,
  scope: ReviewDiffScope,
  commitSha: string | null,
  enabled: boolean
): UseQueryResult<PullRequestDiff> {
  return useQuery({
    queryKey: reviewWorkspaceQueryKeys.diff(
      intentId,
      bindingId,
      scope,
      commitSha
    ),
    queryFn: ({ signal }) =>
      getReviewDiff(intentId, bindingId, scope, commitSha ?? undefined, signal),
    // scope=commit без выбранного коммита — невалидный запрос, не дёргаем API.
    enabled: enabled && (scope === "request" || commitSha !== null),
    staleTime: 60_000
  });
}

export function useReviewPullRequestQuery(
  intentId: string,
  bindingId: string,
  enabled: boolean
): UseQueryResult<PullRequestHeader> {
  return useQuery({
    queryKey: reviewWorkspaceQueryKeys.pullRequest(intentId, bindingId),
    queryFn: ({ signal }) => getReviewPullRequest(intentId, bindingId, signal),
    enabled,
    staleTime: 60_000
  });
}

export function useReviewCommitsQuery(
  intentId: string,
  bindingId: string,
  enabled: boolean
): UseQueryResult<PullRequestCommit[]> {
  return useQuery({
    queryKey: reviewWorkspaceQueryKeys.commits(intentId, bindingId),
    queryFn: ({ signal }) => listReviewCommits(intentId, bindingId, signal),
    enabled,
    staleTime: 60_000
  });
}
