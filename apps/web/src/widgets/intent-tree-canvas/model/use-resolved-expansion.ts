import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";

import {
  fetchIntentLinksSummary,
  intentLinksSummaryQueryKeys
} from "@/entities/intent";

import {
  EMPTY_EXPANSION,
  expandResolved,
  type ResolvedExpansion
} from "./resolved-expansion";

/**
 * React-query wrapper around {@link expandResolved}. Keyed under the
 * links-summary prefix so realtime link add/remove invalidations refresh the
 * resolved overlay too. Disabled (and thus zero-cost) while the toggle is off.
 */
export function useResolvedExpansion(
  enabled: boolean,
  activeIds: readonly string[]
): ResolvedExpansion {
  const cacheKey = useMemo(() => [...activeIds].sort().join("|"), [activeIds]);

  const query = useQuery({
    queryKey: [...intentLinksSummaryQueryKeys.all, "resolved-canvas", cacheKey],
    queryFn: ({ signal }) => {
      const ids = cacheKey.length === 0 ? [] : cacheKey.split("|");
      return expandResolved(ids, fetchIntentLinksSummary, signal);
    },
    enabled: enabled && cacheKey.length > 0
  });

  return query.data ?? EMPTY_EXPANSION;
}
