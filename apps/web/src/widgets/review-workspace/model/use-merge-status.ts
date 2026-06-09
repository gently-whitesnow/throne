import { useCallback, useEffect, useState } from "react";

import {
  getPullRequestMergeStatus,
  type PullRequestMergeStatus
} from "@/entities/review-workspace";

interface UseMergeStatusResult {
  status: PullRequestMergeStatus | null;
  loading: boolean;
  error: Error | null;
  refresh: () => void;
}

/**
 * Loads the PR/MR mergeability + checks snapshot for the merge control. Reloads
 * whenever `nonce` changes — the overlay bumps it after a sync so the indicator
 * reflects freshly-pushed comments / rebased branches.
 */
export function useMergeStatus(
  intentId: string,
  bindingId: string,
  enabled: boolean,
  nonce: number
): UseMergeStatusResult {
  const [status, setStatus] = useState<PullRequestMergeStatus | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const refresh = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  useEffect(() => {
    if (!enabled) {
      setStatus(null);
      return;
    }
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getPullRequestMergeStatus(intentId, bindingId, controller.signal)
      .then((next) => {
        setStatus(next);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setError(err instanceof Error ? err : new Error(String(err)));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => {
      controller.abort();
    };
  }, [intentId, bindingId, enabled, nonce, reloadKey]);

  return { status, loading, error, refresh };
}
