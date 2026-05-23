import type { RepositoriesComponents } from "@/shared/api";

export type PullRequestComment =
  RepositoriesComponents["schemas"]["PullRequestCommentDto"];

export type PullRequestSyncResult =
  RepositoriesComponents["schemas"]["PullRequestSyncResultDto"];

/**
 * Stable ordering for the comments feed (ascending by `created_at`). Slice 1
 * loads the entire feed in one request — server-side pagination is a later
 * intent — so consumers sort once and render.
 */
export function compareComments(
  a: PullRequestComment,
  b: PullRequestComment
): number {
  return a.created_at.localeCompare(b.created_at);
}
