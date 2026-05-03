import { useCallback, useEffect, useState } from "react";

import type { DreamRun } from "@/entities/dream-run";
import { dreamEndpoints, HttpError, httpGet } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";

export type PendingRunsState =
  | { kind: "loading" }
  | { kind: "ready"; items: DreamRun[] }
  | { kind: "error"; message: string };

export function usePendingDreamRuns(): {
  state: PendingRunsState;
  refresh: () => void;
} {
  const [state, setState] = useState<PendingRunsState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    httpGet<DreamRun[]>(
      dreamEndpoints.listPendingDreamRuns(),
      controller.signal
    )
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить pending runs (${String(err.status)}).`
            : "Не удалось загрузить pending runs.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const refresh = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  useRealtimeEvent("dream.run_created", refresh);
  useRealtimeEvent("dream.run_closed", refresh);
  useRealtimeEvent("dream.proposal_created", refresh);
  useRealtimeEvent("dream.proposal_applied", refresh);
  useRealtimeEvent("dream.proposal_skipped", refresh);

  return { state, refresh };
}
