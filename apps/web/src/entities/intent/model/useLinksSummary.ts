import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";

import type { IntentLinksSummaryEntry } from "@/entities/intent";

import { fetchIntentLinksSummary } from "../api/links-summary";

export type LinksSummaryMap = ReadonlyMap<string, IntentLinksSummaryEntry>;

const EMPTY_MAP: LinksSummaryMap = new Map<string, IntentLinksSummaryEntry>();

export const intentLinksSummaryQueryKeys = {
  all: ["intent-links-summary"] as const,
  byIds: (cacheKey: string) =>
    [...intentLinksSummaryQueryKeys.all, cacheKey] as const
};

/**
 * Fetches the link-summary map for the supplied intent ids and inserts it into
 * the shared react-query cache. Realtime invalidation (`intent.link_added` /
 * `intent.link_removed`) живёт в `RealtimeQueryBridge` и сбрасывает префикс
 * `intentLinksSummaryQueryKeys.all`, поэтому здесь нет локальной подписки.
 */
export function useLinksSummary(ids: readonly string[]): LinksSummaryMap {
  const cacheKey = useMemo(() => [...ids].sort().join("|"), [ids]);

  const query = useQuery({
    queryKey: intentLinksSummaryQueryKeys.byIds(cacheKey),
    queryFn: async ({ signal }) => {
      const idList = cacheKey.length === 0 ? [] : cacheKey.split("|");
      const entries = await fetchIntentLinksSummary(idList, signal);
      const map = new Map<string, IntentLinksSummaryEntry>();
      for (const entry of entries) map.set(entry.intent_id, entry);
      return map;
    },
    enabled: cacheKey.length > 0
  });

  return query.data ?? EMPTY_MAP;
}
