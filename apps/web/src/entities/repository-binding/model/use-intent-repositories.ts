import { useCallback, useEffect, useState } from "react";

import { useRealtimeEvent } from "@/shared/realtime";

import { listIntentRepositories } from "../api/repository-bindings-api";
import { subscribeIntentRepositoriesRefresh } from "./refresh-notifier";
import { compareBindings } from "./selectors";
import type { CloneStatus, RepositoryBinding } from "./types";

export interface IntentRepositoriesState {
  bindings: RepositoryBinding[];
  isLoading: boolean;
  error: Error | null;
  /** Force a re-fetch (e.g. after manual sync). */
  refresh: () => void;
}

const EMPTY: RepositoryBinding[] = [];

/**
 * Loads the binding list for an intent and keeps it fresh via realtime events:
 *
 *  - `intent.repository_bound` — replace / insert binding.
 *  - `intent.repository_unbound` — drop binding by id.
 *  - `intent.repository_clone_progress` — patch `clone_status` (+ optional
 *    `clone_error`) in place; no full refetch, the binding aggregate stays
 *    server-of-record for everything else.
 *
 * Events for other intents are ignored.
 */
export function useIntentRepositories(
  intentId: string | null
): IntentRepositoriesState {
  const [bindings, setBindings] = useState<RepositoryBinding[]>(EMPTY);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (intentId === null) {
      setBindings(EMPTY);
      setIsLoading(false);
      setError(null);
      return;
    }

    const controller = new AbortController();
    setIsLoading(true);
    setError(null);

    listIntentRepositories(intentId, controller.signal)
      .then((list) => {
        setBindings([...list].sort(compareBindings));
        setIsLoading(false);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setError(err instanceof Error ? err : new Error(String(err)));
        setIsLoading(false);
      });

    return () => {
      controller.abort();
    };
  }, [intentId, reloadKey]);

  useEffect(() => {
    if (intentId === null) return undefined;
    return subscribeIntentRepositoriesRefresh((changedIntentId, patch) => {
      if (changedIntentId === intentId) {
        if (patch !== undefined) {
          setBindings((prev) =>
            prev
              .map((b) =>
                b.id === patch.binding_id
                  ? {
                      ...b,
                      pull_request_state:
                        patch.pull_request_state !== undefined
                          ? patch.pull_request_state
                          : b.pull_request_state,
                      last_synced_at:
                        patch.last_synced_at !== undefined
                          ? patch.last_synced_at
                          : b.last_synced_at
                    }
                  : b
              )
              .sort(compareBindings)
          );
        }
        setReloadKey((v) => v + 1);
      }
    });
  }, [intentId]);

  const onBound = useCallback(
    (binding: RepositoryBinding) => {
      if (intentId === null || binding.intent_id !== intentId) return;
      setBindings((prev) => {
        const without = prev.filter((b) => b.id !== binding.id);
        return [...without, binding].sort(compareBindings);
      });
    },
    [intentId]
  );
  // prettier-ignore
  useRealtimeEvent("intent.repository_bound", onBound);

  const onUnbound = useCallback(
    (payload: { intent_id: string; binding_id: string }) => {
      if (intentId === null || payload.intent_id !== intentId) return;
      setBindings((prev) => prev.filter((b) => b.id !== payload.binding_id));
    },
    [intentId]
  );
  // prettier-ignore
  useRealtimeEvent("intent.repository_unbound", onUnbound);

  const onCloneProgress = useCallback(
    (payload: {
      intent_id: string;
      binding_id: string;
      status: unknown;
      error?: string;
    }) => {
      if (intentId === null || payload.intent_id !== intentId) return;
      setBindings((prev) =>
        prev
          .map((b) => {
            if (b.id !== payload.binding_id) return b;
            return {
              ...b,
              clone_status: payload.status as CloneStatus,
              clone_error: payload.error
            };
          })
          .sort(compareBindings)
      );
    },
    [intentId]
  );
  // prettier-ignore
  useRealtimeEvent("intent.repository_clone_progress", onCloneProgress);

  const refresh = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  return { bindings, isLoading, error, refresh };
}
