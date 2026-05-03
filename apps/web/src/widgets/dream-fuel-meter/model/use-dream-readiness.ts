import { useCallback, useEffect, useState } from "react";

import type { DreamReadiness } from "@/entities/dream-readiness";
import { dreamEndpoints, HttpError, httpGet } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";

export type DreamReadinessState =
  | { kind: "loading" }
  | { kind: "ready"; data: DreamReadiness }
  | { kind: "error"; message: string };

export function useDreamReadiness(): {
  state: DreamReadinessState;
  refresh: () => void;
} {
  const [state, setState] = useState<DreamReadinessState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    httpGet<DreamReadiness>(
      dreamEndpoints.getDreamReadiness(),
      controller.signal
    )
      .then((data) => {
        setState({ kind: "ready", data });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить readiness (${String(err.status)}).`
            : "Не удалось загрузить readiness.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const refresh = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  useRealtimeEvent("dream.fuel_changed", refresh);
  useRealtimeEvent("dream.run_created", refresh);
  useRealtimeEvent("dream.run_closed", refresh);

  return { state, refresh };
}
