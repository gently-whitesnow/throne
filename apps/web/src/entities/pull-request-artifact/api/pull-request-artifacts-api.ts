import { httpGet, repositoriesEndpoints } from "@/shared/api";

import type { PullRequestArtifact } from "../model/types";

export function getPullRequestArtifact(
  bindingId: string,
  type: string,
  signal?: AbortSignal
): Promise<PullRequestArtifact> {
  return httpGet<PullRequestArtifact>(
    repositoriesEndpoints.getPullRequestArtifact(bindingId, type),
    signal
  );
}

export function listPullRequestArtifacts(
  bindingId: string,
  signal?: AbortSignal
): Promise<PullRequestArtifact[]> {
  return httpGet<PullRequestArtifact[]>(
    repositoriesEndpoints.listPullRequestArtifacts(bindingId),
    signal
  );
}
