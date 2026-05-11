import { httpGet, httpPost, meEndpoints } from "@/shared/api";

import type { McpTokenIssued, McpTokenMeta } from "../model/types";

export function fetchMcpTokenMeta(signal?: AbortSignal): Promise<McpTokenMeta> {
  return httpGet<McpTokenMeta>(meEndpoints.getMcpToken(), signal);
}

export function issueMcpToken(signal?: AbortSignal): Promise<McpTokenIssued> {
  return httpPost<McpTokenIssued>(meEndpoints.regenerateMcpToken(), {}, signal);
}
