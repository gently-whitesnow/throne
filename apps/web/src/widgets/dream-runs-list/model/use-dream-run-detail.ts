import { useCallback, useEffect, useState } from "react";

import type { DreamRunDetail } from "@/entities/dream-run";
import { dreamEndpoints, HttpError, httpGet } from "@/shared/api";

export type DreamRunDetailState =
  | { kind: "loading" }
  | { kind: "ready"; data: DreamRunDetail }
  | { kind: "error"; message: string };

export function useDreamRunDetail(runId: string | null): {
  state: DreamRunDetailState | null;
  refresh: () => void;
  setData: (data: DreamRunDetail) => void;
} {
  const [state, setState] = useState<DreamRunDetailState | null>(
    runId ? { kind: "loading" } : null
  );
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!runId) {
      setState(null);
      return;
    }
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<DreamRunDetail>(
      dreamEndpoints.getDreamRun(runId),
      controller.signal
    )
      .then((data) => {
        setState({ kind: "ready", data });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить run (${String(err.status)}).`
            : "Не удалось загрузить run.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [runId, reloadKey]);

  const refresh = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);
  const setData = useCallback((data: DreamRunDetail) => {
    setState({ kind: "ready", data });
  }, []);

  return { state, refresh, setData };
}
