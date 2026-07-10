import { useQueries } from "@tanstack/react-query";

import {
  boardCardsQueryKeys,
  fetchBoardCards
} from "@/entities/task-tracker-card";

import type { BoardRow } from "./use-context-rows";

const STALE_MS = 5 * 60_000;

// Selecting a board row opens the read-only card browser (ADR-0052), so the
// counter next to it must reflect that browser's card count, not the ADR-0052
// attached-intent facet. Card counts come from the tracker per board, cached in
// the same react-query slot the browser fills — a warm sidebar visit reuses the
// browser's fetch and vice versa.
export function useBoardCardCounts(rows: BoardRow[]): Map<string, number> {
  const results = useQueries({
    queries: rows.map((row) => ({
      queryKey: boardCardsQueryKeys.list(row.tracker, row.boardId),
      queryFn: ({ signal }: { signal?: AbortSignal }) =>
        fetchBoardCards(row.tracker, row.boardId, signal),
      staleTime: STALE_MS,
      retry: false
    }))
  });

  const counts = new Map<string, number>();
  results.forEach((result, index) => {
    if (result.data) {
      counts.set(rows[index].key, result.data.length);
    }
  });
  return counts;
}
