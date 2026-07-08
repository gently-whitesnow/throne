import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { HttpError } from "@/shared/api";

import type { TaskTrackerCard } from "../model/types";
import { fetchBoardCard, fetchBoardCards } from "./board-cards-api";

export const boardCardsQueryKeys = {
  all: ["task-tracker-board-cards"] as const,
  list: (tracker: string, boardId: string) =>
    [...boardCardsQueryKeys.all, "list", tracker, boardId] as const,
  card: (tracker: string, boardId: string, cardId: string) =>
    [...boardCardsQueryKeys.all, "card", tracker, boardId, cardId] as const
};

/**
 * Degradation codes are deterministic (blocked / not-connected / unsupported /
 * gone) — retrying an auth/tariff/not-found response never turns it into a 200,
 * so we only retry transient failures (network / 5xx that isn't the offline 502).
 */
function isTerminalStatus(error: unknown): boolean {
  return (
    error instanceof HttpError &&
    [402, 404, 409, 422, 502].includes(error.status)
  );
}

const retry = (count: number, error: Error): boolean =>
  !isTerminalStatus(error) && count < 2;

export function useBoardCardsQuery(
  tracker: string,
  boardId: string
): UseQueryResult<TaskTrackerCard[]> {
  return useQuery({
    queryKey: boardCardsQueryKeys.list(tracker, boardId),
    queryFn: ({ signal }) => fetchBoardCards(tracker, boardId, signal),
    retry
  });
}

export function useBoardCardQuery(
  tracker: string,
  boardId: string,
  cardId: string | null
): UseQueryResult<TaskTrackerCard> {
  return useQuery({
    queryKey:
      cardId !== null
        ? boardCardsQueryKeys.card(tracker, boardId, cardId)
        : [...boardCardsQueryKeys.all, "card", tracker, boardId, "none"],
    queryFn: ({ signal }) => {
      if (cardId === null) {
        throw new Error("useBoardCardQuery: cardId is required");
      }
      return fetchBoardCard(tracker, boardId, cardId, signal);
    },
    enabled: cardId !== null,
    retry
  });
}
