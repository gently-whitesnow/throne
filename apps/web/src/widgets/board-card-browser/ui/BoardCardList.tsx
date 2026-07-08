import type { TaskTrackerCard } from "@/entities/task-tracker-card";

import { BoardCardRow } from "./BoardCardRow";

interface BoardCardListProps {
  cards: TaskTrackerCard[];
  selectedCardId: string | null;
  onSelect: (cardId: string) => void;
}

/** Scrollable list of a board's active cards. */
export function BoardCardList({
  cards,
  selectedCardId,
  onSelect
}: BoardCardListProps) {
  return (
    <ul className="m-0 flex flex-col gap-1.5 p-0">
      {cards.map((card) => (
        <BoardCardRow
          key={card.card_id}
          card={card}
          selected={card.card_id === selectedCardId}
          onSelect={onSelect}
        />
      ))}
    </ul>
  );
}
