import { httpGet, taskTrackersEndpoints } from "@/shared/api";

import type {
  TaskTrackerBoardCardsResponse,
  TaskTrackerCard
} from "../model/types";

/**
 * Active cards of a board as the browser lists them. Rows omit `description`
 * (the provider fills it only on the single-card read). Archived cards are
 * excluded server-side.
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

/** Single card with its `description` populated (read-only, non-authoritative). */
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
