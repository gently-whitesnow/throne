import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";

import {
  dreamsQueryKeys,
  type DreamSession,
  useDreamSessionsList
} from "@/entities/dream-session";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: DreamSession[]; nextCursor?: string }
  | { kind: "error"; message: string };

export function useDreamSessions(): {
  state: LoadState;
  reload: () => void;
} {
  const qc = useQueryClient();
  const query = useDreamSessionsList({ limit: 50 });

  const reload = useCallback(() => {
    void qc.invalidateQueries({ queryKey: dreamsQueryKeys.all });
  }, [qc]);

  const state: LoadState = query.isPending
    ? { kind: "loading" }
    : query.error
      ? { kind: "error", message: query.error.message }
      : {
          kind: "ready",
          items: query.data.items,
          nextCursor: query.data.next_cursor
        };

  return { state, reload };
}
