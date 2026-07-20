import {
  cardTextPreview,
  type TaskTrackerCard
} from "@/entities/task-tracker-card";

interface BoardCardRowProps {
  card: TaskTrackerCard;
  selected: boolean;
  onSelect: (cardId: string) => void;
}

/** Neutral, read-only list row: compact text preview + its column (muted). */
export function BoardCardRow({ card, selected, onSelect }: BoardCardRowProps) {
  return (
    <li className="list-none">
      <button
        type="button"
        aria-pressed={selected}
        onClick={() => {
          onSelect(card.card_id);
        }}
        className={`flex w-full flex-col gap-0.5 rounded-md border px-3 py-2 text-left transition-[background-color,border-color,color] active:scale-[0.99] ${
          selected
            ? "border-primary/40 bg-primary/10 text-primary"
            : "border-base-300 bg-base-100 text-base-content hover:border-primary/30 hover:bg-base-200/60"
        }`}
      >
        <span className="truncate text-sm font-medium leading-tight">
          {cardTextPreview(card.text)}
        </span>
        {card.column_title ? (
          <span
            className={`truncate text-xs ${
              selected ? "text-primary/70" : "text-base-content/50"
            }`}
          >
            {card.column_title}
          </span>
        ) : null}
      </button>
    </li>
  );
}
