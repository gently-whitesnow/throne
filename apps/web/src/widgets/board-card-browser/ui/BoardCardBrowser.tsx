import { useState } from "react";

import { useBoardCardsQuery } from "@/entities/task-tracker-card";

import { boardCardsErrorMessage } from "../model/degradation";
import { BoardCardDetail } from "./BoardCardDetail";
import { BoardCardList } from "./BoardCardList";

interface BoardCardBrowserProps {
  tracker: string;
  boardId: string;
}

/**
 * Card browser for a sidebar board: lists a board's active cards, opens one
 * read-only, and offers «Создать интент с этой карточкой». Replaces the intent
 * board/canvas in the middle pane while a board context is selected (ADR-0052).
 * Degraded upstream states resolve to friendly copy, never a blank pane.
 */
export function BoardCardBrowser({ tracker, boardId }: BoardCardBrowserProps) {
  const cardsQuery = useBoardCardsQuery(tracker, boardId);
  const [selectedCardId, setSelectedCardId] = useState<string | null>(null);

  const cards = cardsQuery.data ?? [];
  const selectedCard =
    selectedCardId === null
      ? null
      : (cards.find((card) => card.card_id === selectedCardId) ?? null);

  return (
    <section
      className="flex min-h-0 min-w-0 flex-col overflow-hidden bg-base-100"
      aria-label={`Карточки доски ${boardId}`}
    >
      <header className="flex flex-shrink-0 items-center gap-2 border-b border-base-300 px-4 py-3">
        <h3 className="m-0 truncate text-sm font-semibold leading-tight">
          {boardId}
        </h3>
        {!cardsQuery.isLoading && !cardsQuery.isError ? (
          <span className="text-xs tabular-nums text-base-content/50">
            {cards.length}
          </span>
        ) : null}
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto p-3">
        {cardsQuery.isLoading ? (
          <p className="m-0 text-sm text-base-content/60">
            Загружаем карточки…
          </p>
        ) : cardsQuery.isError ? (
          <p role="alert" className="m-0 text-sm text-error">
            {boardCardsErrorMessage(cardsQuery.error)}
          </p>
        ) : selectedCard ? (
          <BoardCardDetail
            tracker={tracker}
            boardId={boardId}
            listCard={selectedCard}
            onBack={() => {
              setSelectedCardId(null);
            }}
          />
        ) : cards.length === 0 ? (
          <p className="m-0 text-sm text-base-content/60">
            На доске нет активных карточек.
          </p>
        ) : (
          <BoardCardList
            cards={cards}
            selectedCardId={selectedCardId}
            onSelect={setSelectedCardId}
          />
        )}
      </div>
    </section>
  );
}
