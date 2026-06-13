import { httpGet, promptPartPatchesEndpoints } from "@/shared/api";

import type {
  PromptPartPatchPage,
  PromptPartPatchStatus
} from "../model/types";

export interface ListPromptPartPatchesQuery {
  status?: PromptPartPatchStatus;
  targetScope?: string;
  targetKey?: string;
  limit?: number;
  cursor?: string;
}

export function listPromptPartPatches(
  query: ListPromptPartPatchesQuery,
  signal?: AbortSignal
): Promise<PromptPartPatchPage> {
  const params = new URLSearchParams();
  if (query.status) params.set("status", query.status);
  if (query.targetScope) params.set("target_scope", query.targetScope);
  if (query.targetKey) params.set("target_key", query.targetKey);
  if (typeof query.limit === "number") params.set("limit", String(query.limit));
  if (query.cursor) params.set("cursor", query.cursor);
  const qs = params.toString();
  const url = `${promptPartPatchesEndpoints.listPromptPartPatches()}${qs ? `?${qs}` : ""}`;
  return httpGet<PromptPartPatchPage>(url, signal);
}
