import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";

import {
  mcpTokenQueryKeys,
  useMcpTokenMetaQuery,
  type McpTokenMeta
} from "@/entities/mcp-token";
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
  const qc = useQueryClient();
  const query = useMcpTokenMetaQuery();

  const setMeta = useCallback(
    (meta: McpTokenMeta) => {
      qc.setQueryData<McpTokenMeta>(mcpTokenQueryKeys.meta(), meta);
    },
    [qc]
  );

  const refresh = useCallback(() => {
    void qc.invalidateQueries({ queryKey: mcpTokenQueryKeys.meta() });
  }, [qc]);

  const state: McpTokenMetaState = query.isPending
    ? { kind: "loading" }
    : query.error
      ? {
          kind: "error",
          message:
            query.error instanceof HttpError
              ? `Не удалось загрузить токен (${String(query.error.status)}).`
              : "Не удалось загрузить токен."
        }
      : { kind: "ready", data: query.data };

  return { state, setMeta, refresh };
}
