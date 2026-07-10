import { ArrowLeft, Plus } from "lucide-react";

import {
  useBoardCardQuery,
  type TaskTrackerCard
} from "@/entities/task-tracker-card";
import { Button, MarkdownView } from "@/shared/ui";

import { boardCardsErrorMessage } from "../model/degradation";
import { useCreateIntentWithCard } from "../model/use-create-intent-with-card";

interface BoardCardDetailProps {
  tracker: string;
  boardId: string;
  /** The list row we opened from — carries title/coordinates before the fetch resolves. */
  listCard: TaskTrackerCard;
  onBack: () => void;
}

/** Read-only card detail (title + markdown description) with the create action. */
export function BoardCardDetail({
  tracker,
  boardId,
  listCard,
  onBack
}: BoardCardDetailProps) {
  const cardQuery = useBoardCardQuery(tracker, boardId, listCard.card_id);
  const { createWithCard, pending, error } = useCreateIntentWithCard(tracker);

  const card = cardQuery.data ?? listCard;

  return (
    <div className="flex min-h-0 flex-col gap-3">
      <button
        type="button"
        onClick={onBack}
        className="inline-flex w-fit items-center gap-1.5 text-xs font-medium text-base-content/60 transition-colors hover:text-base-content"
      >
        <ArrowLeft aria-hidden size={14} strokeWidth={2} />К списку карточек
      </button>

      <div className="flex flex-col gap-1">
        <h4 className="m-0 text-balance text-base font-semibold leading-snug">
          {card.web_url ? (
            <a
              href={card.web_url}
              target="_blank"
              rel="noopener noreferrer"
              data-testid={`board-card-link-${card.card_id}`}
              className="text-base-content hover:underline focus-visible:underline"
            >
              {card.title}
            </a>
          ) : (
            card.title
          )}
        </h4>
        {card.column_title ? (
          <span className="text-xs text-base-content/50">
            {card.column_title}
          </span>
        ) : null}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {cardQuery.isLoading ? (
          <p className="m-0 text-sm text-base-content/60">
            Загружаем карточку…
          </p>
        ) : cardQuery.isError ? (
          <p role="alert" className="m-0 text-sm text-error">
            {boardCardsErrorMessage(cardQuery.error)}
          </p>
        ) : card.description ? (
          <MarkdownView
            markdown={card.description}
            className="rounded-md border border-base-200 bg-base-200/40 px-3 py-2 text-sm"
          />
        ) : (
          <p className="m-0 text-sm text-base-content/50">Без описания</p>
        )}
      </div>

      {error ? (
        <p role="alert" className="m-0 text-xs text-error">
          {error}
        </p>
      ) : null}

      <Button
        variant="primary"
        disabled={pending}
        icon={<Plus aria-hidden size={16} strokeWidth={2.4} />}
        onClick={() => {
          void createWithCard(card);
        }}
      >
        {pending ? "Создаём…" : "Создать интент с этой карточкой"}
      </Button>
    </div>
  );
}
