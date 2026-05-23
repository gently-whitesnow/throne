import { useCallback, useEffect, useState } from "react";

import { fetchGitProvidersStatus } from "../api/git-providers-status-api";
import type { GitProvidersStatus } from "./types";

export interface GitProvidersStatusState {
  status: GitProvidersStatus | null;
  isLoading: boolean;
  error: Error | null;
  refresh: () => void;
}

/**
 * One-shot fetch of the auth status for every configured provider CLI.
 * The settings page calls `refresh` manually after the user runs `gh auth login`
 * — we don't poll. There is no realtime event for auth changes in slice 1.
 */
export function useGitProvidersStatus(): GitProvidersStatusState {
  const [status, setStatus] = useState<GitProvidersStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setIsLoading(true);
    setError(null);

    fetchGitProvidersStatus(controller.signal)
      .then((next) => {
        setStatus(next);
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
  }, [reloadKey]);

  const refresh = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  return { status, isLoading, error, refresh };
}
