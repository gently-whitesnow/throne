import { httpGet, taskTrackersEndpoints } from "@/shared/api";

import type {
  TaskTrackerBoardCardsResponse,
  TaskTrackerCard
} from "../model/types";

/**
 * Active cards of a board as the browser lists them. Each row includes canonical
 * tracker text; archived cards are excluded server-side.
 */
export function fetchBoardCards(
  tracker: string,
  boardId: string,
  signal?: AbortSignal
): Promise<TaskTrackerCard[]> {
  return httpGet<TaskTrackerBoardCardsResponse>(
    taskTrackersEndpoints.listBoardCards(tracker, boardId),
    signal
  ).then((response) => response.cards);
}

/**
 * Search cards inside a board for the attach-card combobox. Empty `query` asks
 * the backend for the most recently touched cards (top-N by `updated_at desc`);
 * a non-empty one is forwarded to the tracker's own text filter. Server clamps
 * `limit`; no local cache — every keystroke round-trips to the tracker.
 */
export function searchBoardCards(
  tracker: string,
  boardId: string,
  params: { query?: string; limit?: number },
  signal?: AbortSignal
): Promise<TaskTrackerCard[]> {
  const search = new URLSearchParams();
  if (params.query && params.query.length > 0) {
    search.set("query", params.query);
  }
  if (params.limit !== undefined) {
    search.set("limit", String(params.limit));
  }
  const suffix = search.size > 0 ? `?${search.toString()}` : "";
  return httpGet<TaskTrackerBoardCardsResponse>(
    `${taskTrackersEndpoints.searchBoardCards(tracker, boardId)}${suffix}`,
    signal
  ).then((response) => response.cards);
}

/** Single card with its full `text` (read-only, non-authoritative). */
export function fetchBoardCard(
  tracker: string,
  boardId: string,
  cardId: string,
  signal?: AbortSignal
): Promise<TaskTrackerCard> {
  return httpGet<TaskTrackerCard>(
    taskTrackersEndpoints.getBoardCard(tracker, boardId, cardId),
    signal
  );
}
