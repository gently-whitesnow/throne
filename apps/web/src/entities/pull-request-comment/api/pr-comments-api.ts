import {
  httpDelete,
  httpGet,
  httpPatch,
  httpPost,
  repositoriesEndpoints
} from "@/shared/api";

import type {
  PullRequestComment,
  PullRequestSyncResult,
  ReviewThread
} from "../model/types";

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
 * Manual refresh (ADR-0024): the endpoint is synchronous, returns the
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

/**
 * Deletes a review comment at the provider. `threadId` is appended as a query
 * param when present — GitLab needs the discussion id to locate the note,
 * GitHub ignores it.
 */
export function deletePullRequestComment(
  intentId: string,
  bindingId: string,
  commentId: string,
  threadId?: string | null,
  signal?: AbortSignal
): Promise<void> {
  const base = repositoriesEndpoints.deleteIntentRepositoryPullRequestComment(
    intentId,
    bindingId,
    commentId
  );
  const path =
    threadId != null && threadId.length > 0
      ? `${base}?thread_id=${encodeURIComponent(threadId)}`
      : base;
  return httpDelete(path, signal);
}

/**
 * Resolves (`resolved: true`) or reopens a review thread at the provider and
 * returns the state echoed back. Throne stores no local status — callers refresh
 * the feed afterwards to read the new `resolved` value off each comment.
 */
export function updateReviewThread(
  intentId: string,
  bindingId: string,
  threadId: string,
  resolved: boolean,
  signal?: AbortSignal
): Promise<ReviewThread> {
  return httpPatch<ReviewThread>(
    repositoriesEndpoints.updateIntentRepositoryReviewThread(
      intentId,
      bindingId,
      threadId
    ),
    { resolved },
    signal
  );
}
