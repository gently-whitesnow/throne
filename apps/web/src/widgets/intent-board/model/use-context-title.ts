import { useMemo } from "react";

import { useIntentContexts } from "@/entities/intent";
import { boardContextParts } from "@/shared/lib";

import { contextTitle } from "./board-helpers";

/**
 * Human label for the board header. Board groups carry their display title only in the contexts
 * aggregate (the context key holds the raw board id), so resolve it from there; everything else
 * falls back to the static {@link contextTitle}.
 */
export function useContextTitle(context: string | null): string {
  const contextsData = useIntentContexts().data;
  return useMemo(() => {
    const parts = boardContextParts(context);
    if (parts && contextsData) {
      const board = contextsData.boards.find(
        (b) => b.tracker === parts.tracker && b.board_id === parts.boardId
      );
      if (board) return board.board_title ?? board.board_id;
    }
    return contextTitle(context);
  }, [context, contextsData]);
}
