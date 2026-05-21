import { useEffect, useMemo, useState } from "react";

import type { IntentLinksSummaryEntry } from "@/entities/intent";
import { useRealtimeEvent } from "@/shared/realtime";

import { fetchIntentLinksSummary } from "../api/links-summary";

export type LinksSummaryMap = ReadonlyMap<string, IntentLinksSummaryEntry>;

const EMPTY_MAP: LinksSummaryMap = new Map<string, IntentLinksSummaryEntry>();

/**
 * Fetches the link-summary map for the supplied intent ids and refetches when
 * the realtime layer reports a link mutation. Resolves to an empty map before
 * the first response and on errors — the board always renders; badges appear
 * once the summary settles.
 */
export function useLinksSummary(ids: readonly string[]): LinksSummaryMap {
  const cacheKey = useMemo(() => [...ids].sort().join("|"), [ids]);
  const [map, setMap] = useState<LinksSummaryMap>(EMPTY_MAP);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (cacheKey.length === 0) {
      setMap(EMPTY_MAP);
      return;
    }
    const idList = cacheKey.split("|");
    const controller = new AbortController();
    fetchIntentLinksSummary(idList, controller.signal)
      .then((entries) => {
        const next = new Map<string, IntentLinksSummaryEntry>();
        for (const entry of entries) {
          next.set(entry.intent_id, entry);
        }
        setMap(next);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        // Board renders without badges; log so it surfaces in dev.
        console.warn("links-summary fetch failed", err);
      });
    return () => {
      controller.abort();
    };
  }, [cacheKey, reloadKey]);

  useRealtimeEvent("intent.link_added", () => {
    setReloadKey((v) => v + 1);
  });
  useRealtimeEvent("intent.link_removed", () => {
    setReloadKey((v) => v + 1);
  });

  return map;
}
