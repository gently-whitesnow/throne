import { useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";

import {
  intentRepositoriesQueryKeys,
  useIntentRepositoriesQuery
} from "../api/intent-repositories-queries";
import type { RepositoryBinding } from "./types";

export interface IntentRepositoriesState {
  bindings: RepositoryBinding[];
  isLoading: boolean;
  error: Error | null;
  /** Force a re-fetch (e.g. after manual sync). */
  refresh: () => void;
}

const EMPTY: RepositoryBinding[] = [];

/**
 * Thin wrapper over `useIntentRepositoriesQuery`. Realtime updates
 * (`intent.repository_bound`, `intent.repository_unbound`,
 * `intent.repository_clone_progress`) are wired centrally in
 * `RealtimeQueryBridge`.
 */
export function useIntentRepositories(
  intentId: string | null
): IntentRepositoriesState {
  const queryClient = useQueryClient();
  const query = useIntentRepositoriesQuery(intentId);

  const refresh = useCallback(() => {
    if (intentId === null) return;
    void queryClient.invalidateQueries({
      queryKey: intentRepositoriesQueryKeys.list(intentId)
    });
  }, [queryClient, intentId]);

  return {
    bindings: query.data ?? EMPTY,
    isLoading: query.isPending && intentId !== null,
    error: query.error instanceof Error ? query.error : null,
    refresh
  };
}
