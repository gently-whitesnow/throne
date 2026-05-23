import { useCallback, useEffect, useRef, useState } from "react";

import { fetchWorkspaceSettings } from "../api/workspace-settings-api";
import { isWorkspaceCalculating } from "./types";
import type { WorkspaceSettings } from "./types";

export interface WorkspaceSettingsState {
  settings: WorkspaceSettings | null;
  isLoading: boolean;
  error: Error | null;
  refresh: () => void;
}

const CALCULATING_POLL_MS = 2_000;

/**
 * Loads `/settings/workspace` and, while the server reports
 * `status=calculating`, gently polls every couple of seconds until the size
 * settles. There is no realtime event for this in slice 1 — polling is bounded
 * by the calculating state, so the network stays quiet after the first ready
 * response.
 */
export function useWorkspaceSettings(): WorkspaceSettingsState {
  const [settings, setSettings] = useState<WorkspaceSettings | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;
    setIsLoading(true);
    setError(null);

    const tick = (): void => {
      fetchWorkspaceSettings(controller.signal)
        .then((next) => {
          if (cancelled) return;
          setSettings(next);
          setIsLoading(false);
          if (isWorkspaceCalculating(next.status)) {
            timerRef.current = setTimeout(tick, CALCULATING_POLL_MS);
          }
        })
        .catch((err: unknown) => {
          if (controller.signal.aborted || cancelled) return;
          setError(err instanceof Error ? err : new Error(String(err)));
          setIsLoading(false);
        });
    };

    tick();

    return () => {
      cancelled = true;
      controller.abort();
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [reloadKey]);

  const refresh = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  return { settings, isLoading, error, refresh };
}
