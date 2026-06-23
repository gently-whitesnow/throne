import type { QueryClient } from "@tanstack/react-query";

import { pullRequestArtifactsQueryKeys } from "@/entities/pull-request-artifact";
import { useRealtimeEvent } from "@/shared/realtime";

export function useRepositoryRealtimeEvents(qc: QueryClient): void {
  useRealtimeEvent("pull_request.artifact_updated", (payload) => {
    void qc.invalidateQueries({
      queryKey: pullRequestArtifactsQueryKeys.detail(
        payload.binding_id,
        payload.type
      )
    });
  });
}
