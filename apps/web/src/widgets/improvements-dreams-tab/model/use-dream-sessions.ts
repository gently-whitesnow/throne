import { useCallback, useEffect, useState } from "react";

import {
  type DreamSession,
  type DreamSessionPage,
  listDreamSessions
} from "@/entities/dream-session";
import { useRealtimeEvent } from "@/shared/realtime";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: DreamSession[]; nextCursor?: string }
  | { kind: "error"; message: string };

export function useDreamSessions(): {
  state: LoadState;
  reload: () => void;
} {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    listDreamSessions({ limit: 50 }, controller.signal)
      .then((page: DreamSessionPage) => {
        setState({
          kind: "ready",
          items: page.items,
          nextCursor: page.next_cursor
        });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof Error
              ? err.message
              : "Не удалось загрузить DreamSessions."
        });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  useRealtimeEvent("dream_session.recorded", reload);

  return { state, reload };
}
