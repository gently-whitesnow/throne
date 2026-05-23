import { httpGet, httpPost, repositoriesEndpoints } from "@/shared/api";

import type { PullRequestComment, PullRequestSyncResult } from "../model/types";

export function listPullRequestComments(
  intentId: string,
  bindingId: string,
  since?: string,
  signal?: AbortSignal
): Promise<PullRequestComment[]> {
  const base = repositoriesEndpoints.listIntentRepositoryPullRequestComments(
    intentId,
    bindingId
  );
  const path =
    since !== undefined ? `${base}?since=${encodeURIComponent(since)}` : base;
  return httpGet<PullRequestComment[]>(path, signal);
}

/**
 * Manual refresh (slice 1, ADR-0024): the endpoint is synchronous, returns the
 * full review-comments feed after talking to upstream, and `intent.pr_comment_added`
 * is still fanned out for other open tabs.
 */
export function syncPullRequest(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<PullRequestSyncResult> {
  return httpPost<PullRequestSyncResult>(
    repositoriesEndpoints.syncIntentRepositoryPullRequest(intentId, bindingId),
    {},
    signal
  );
}
