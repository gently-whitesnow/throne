import { httpGet, httpPost, repositoriesEndpoints } from "@/shared/api";

import type {
  MergePullRequestRequest,
  MergePullRequestResult,
  PullRequestMergeStatus
} from "../model/types";

export function getPullRequestMergeStatus(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<PullRequestMergeStatus> {
  return httpGet<PullRequestMergeStatus>(
    repositoriesEndpoints.getIntentRepositoryPullRequestMergeStatus(
      intentId,
      bindingId
    ),
    signal
  );
}

export function mergePullRequest(
  intentId: string,
  bindingId: string,
  body: MergePullRequestRequest,
  signal?: AbortSignal
): Promise<MergePullRequestResult> {
  return httpPost<MergePullRequestResult>(
    repositoriesEndpoints.mergeIntentRepositoryPullRequest(intentId, bindingId),
    body,
    signal
  );
}
