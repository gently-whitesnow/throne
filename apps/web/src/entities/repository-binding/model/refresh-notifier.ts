import type { PullRequestState } from "./types";

export interface IntentRepositoriesRefreshPatch {
  binding_id: string;
  pull_request_state?: PullRequestState | null;
  last_synced_at?: string | null;
}

type Listener = (
  intentId: string,
  patch?: IntentRepositoriesRefreshPatch
) => void;

const listeners = new Set<Listener>();

export function requestIntentRepositoriesRefresh(
  intentId: string,
  patch?: IntentRepositoriesRefreshPatch
) {
  for (const listener of listeners) {
    listener(intentId, patch);
  }
}

export function subscribeIntentRepositoriesRefresh(listener: Listener) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
