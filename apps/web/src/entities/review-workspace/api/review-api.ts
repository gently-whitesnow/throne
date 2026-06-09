import { httpGet, httpPost, repositoriesEndpoints } from "@/shared/api";

import type {
  PullRequestCommit,
  PullRequestDiff,
  PullRequestHeader,
  ReviewDiffScope,
  SubmitReviewCommentRequest,
  SubmittedReviewComment
} from "../model/types";

function buildDiffQuery(scope: ReviewDiffScope, commitSha?: string): string {
  const search = new URLSearchParams({ scope });
  if (scope === "commit" && commitSha !== undefined) {
    search.set("commit_sha", commitSha);
  }
  return `?${search.toString()}`;
}

export function getReviewDiff(
  intentId: string,
  bindingId: string,
  scope: ReviewDiffScope,
  commitSha?: string,
  signal?: AbortSignal
): Promise<PullRequestDiff> {
  const path = `${repositoriesEndpoints.getIntentRepositoryReviewDiff(
    intentId,
    bindingId
  )}${buildDiffQuery(scope, commitSha)}`;
  return httpGet<PullRequestDiff>(path, signal);
}

export function getReviewPullRequest(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<PullRequestHeader> {
  return httpGet<PullRequestHeader>(
    repositoriesEndpoints.getIntentRepositoryPullRequest(intentId, bindingId),
    signal
  );
}

export function listReviewCommits(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<PullRequestCommit[]> {
  return httpGet<PullRequestCommit[]>(
    repositoriesEndpoints.listIntentRepositoryReviewCommits(
      intentId,
      bindingId
    ),
    signal
  );
}

export function submitReviewComment(
  intentId: string,
  bindingId: string,
  body: SubmitReviewCommentRequest,
  signal?: AbortSignal
): Promise<SubmittedReviewComment> {
  return httpPost<SubmittedReviewComment>(
    repositoriesEndpoints.submitIntentRepositoryReviewComment(
      intentId,
      bindingId
    ),
    body,
    signal
  );
}
