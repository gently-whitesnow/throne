import { useCallback, useEffect, useState } from "react";

import type { McpTokenMeta } from "@/entities/mcp-token";
import { fetchMcpTokenMeta } from "@/entities/mcp-token";
import { HttpError } from "@/shared/api";

export type McpTokenMetaState =
  | { kind: "loading" }
  | { kind: "ready"; data: McpTokenMeta }
  | { kind: "error"; message: string };

export function useMcpTokenMeta(): {
  state: McpTokenMetaState;
  setMeta: (meta: McpTokenMeta) => void;
  refresh: () => void;
} {
  const [state, setState] = useState<McpTokenMetaState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    fetchMcpTokenMeta(controller.signal)
      .then((data) => {
        setState({ kind: "ready", data });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить токен (${String(err.status)}).`
            : "Не удалось загрузить токен.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const setMeta = useCallback((meta: McpTokenMeta) => {
    setState({ kind: "ready", data: meta });
  }, []);

  const refresh = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  return { state, setMeta, refresh };
}
