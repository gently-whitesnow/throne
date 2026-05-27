import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { McpTokenMeta } from "../model/types";
import { fetchMcpTokenMeta } from "./mcp-tokens-api";

export const mcpTokenQueryKeys = {
  all: ["mcp-token"] as const,
  meta: () => [...mcpTokenQueryKeys.all, "meta"] as const
};

export function useMcpTokenMetaQuery(): UseQueryResult<McpTokenMeta> {
  return useQuery({
    queryKey: mcpTokenQueryKeys.meta(),
    queryFn: ({ signal }) => fetchMcpTokenMeta(signal)
  });
}
