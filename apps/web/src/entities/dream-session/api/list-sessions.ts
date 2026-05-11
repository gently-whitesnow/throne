import { dreamsEndpoints, httpGet } from "@/shared/api";

import type { DreamSessionPage } from "../model/types";

export interface ListDreamSessionsQuery {
  vendor?: string;
  limit?: number;
  cursor?: string;
}

export function listDreamSessions(
  query: ListDreamSessionsQuery,
  signal?: AbortSignal
): Promise<DreamSessionPage> {
  const params = new URLSearchParams();
  if (query.vendor) params.set("vendor", query.vendor);
  if (typeof query.limit === "number") params.set("limit", String(query.limit));
  if (query.cursor) params.set("cursor", query.cursor);
  const qs = params.toString();
  const url = `${dreamsEndpoints.listDreamSessions()}${qs ? `?${qs}` : ""}`;
  return httpGet<DreamSessionPage>(url, signal);
}
