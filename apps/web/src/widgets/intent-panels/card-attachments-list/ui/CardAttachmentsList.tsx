import { AlertCircle, SquareKanban } from "lucide-react";

import {
  useIntentCardAttachmentsQuery,
  type CardAttachment
} from "@/entities/task-tracker-card";
import { SectionHeading } from "@/shared/ui";

import { AttachCardButton } from "./AttachCardButton";
import { CardAttachmentRow } from "./CardAttachmentRow";

interface CardAttachmentsListProps {
  intentId: string;
}

/**
 * Intent → панель «Карточки»: приложенные карточки-как-read-only-контекст
 * (разворот mirror→attach, ADR-0052). Снапшот non-authoritative, вверх не
 * пишется; `intent.text` остаётся собственным текстом оператора. Realtime у
 * карточек нет by design — список освежает query-invalidation на мутациях
 * attach/detach/refresh (в отличие от `intent.repository_*` с SSE).
 */
export function CardAttachmentsList({ intentId }: CardAttachmentsListProps) {
  const { data, isLoading, error } = useIntentCardAttachmentsQuery(intentId);
  const cards = data ?? [];

  return (
    <section
      aria-label="Карточки"
      className="flex flex-col gap-3"
      data-testid="card-attachments-section"
    >
      <SectionHeading
        icon={<SquareKanban size={14} strokeWidth={2} />}
        title="Карточки"
        count={cards.length > 0 ? cards.length : undefined}
        actions={<AttachCardButton intentId={intentId} />}
      />

      <Body
        isLoading={isLoading}
        error={error}
        cards={cards}
        intentId={intentId}
      />
    </section>
  );
}

interface BodyProps {
  isLoading: boolean;
  error: Error | null;
  cards: CardAttachment[];
  intentId: string;
}

function Body({ isLoading, error, cards, intentId }: BodyProps) {
  if (error) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить карточки: {error.message}</span>
      </p>
    );
  }

  if (cards.length === 0) {
    if (isLoading) {
      return <p className="m-0 text-sm text-base-content/60">Загружаем…</p>;
    }
    return (
      <p className="m-0 text-xs text-base-content/55">Карточки не приложены.</p>
    );
  }

  return (
    <ul className="m-0 flex list-none flex-col gap-2 p-0">
      {cards.map((card) => (
        <CardAttachmentRow key={card.id} intentId={intentId} card={card} />
      ))}
    </ul>
  );
}
