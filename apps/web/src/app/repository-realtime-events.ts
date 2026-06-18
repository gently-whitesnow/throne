import type { QueryClient } from "@tanstack/react-query";

import { repositoriesQueryKeys } from "@/entities/repository";
import { useRealtimeEvent } from "@/shared/realtime";

export function useRepositoryRealtimeEvents(qc: QueryClient): void {
  useRealtimeEvent("repository.registered", () => {
    void qc.invalidateQueries({ queryKey: repositoriesQueryKeys.list() });
  });
  useRealtimeEvent("repository.document_updated", (payload) => {
    const coordinate = {
      provider: payload.provider,
      owner: payload.owner,
      repo: payload.repo
    };
    void qc.invalidateQueries({
      queryKey: repositoriesQueryKeys.document(coordinate, payload.slug)
    });
    void qc.invalidateQueries({
      queryKey: repositoriesQueryKeys.documentVersions(coordinate, payload.slug)
    });
  });
  useRealtimeEvent("pull_request.artifact_updated", () => {
    void qc.invalidateQueries({ queryKey: repositoriesQueryKeys.all });
  });
}
