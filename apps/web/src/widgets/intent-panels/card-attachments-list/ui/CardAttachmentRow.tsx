import { Archive, RefreshCw, SquareKanban, Trash2 } from "lucide-react";

import {
  cardAvailabilityMetaOf,
  cardCoordinateLabel,
  cardTextPreview,
  isCardArchived,
  type CardAttachment
} from "@/entities/task-tracker-card";
import { MarkdownView } from "@/shared/ui";

import { useCardRowActions } from "../model/use-card-row-actions";
import { CardRowMenu } from "./CardRowMenu";

interface CardAttachmentRowProps {
  intentId: string;
  card: CardAttachment;
}

/**
 * Одна строка панели приложенных карточек — «прозрачное окно» в снапшот:
 * координата (tracker · board · card), превью text, бейдж архива, availability-бейдж и
 * read-only Markdown. Содержимое не редактируется (снапшот non-authoritative,
 * вверх не пишется, ADR-0052). Per-row действия — «Обновить» (refresh) и
 * «Отвязать» (detach) в overflow-меню.
 */
export function CardAttachmentRow({ intentId, card }: CardAttachmentRowProps) {
  const coordinate = cardCoordinateLabel(card);
  const availability = cardAvailabilityMetaOf(card);
  const archived = isCardArchived(card);

  const { refresh, detach, refreshing, detaching, busy, error } =
    useCardRowActions({ intentId, card, label: coordinate });

  return (
    <li
      data-testid={`card-attachment-row-${card.id}`}
      className="flex flex-col gap-2 rounded-lg border border-base-300 bg-base-100 px-4 py-3"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex min-w-0 items-center gap-2">
            <span
              aria-hidden
              className="inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
            >
              <SquareKanban size={14} strokeWidth={2} />
            </span>
            {card.web_url ? (
              <a
                href={card.web_url}
                target="_blank"
                rel="noopener noreferrer"
                data-testid={`card-attachment-link-${card.id}`}
                className="truncate text-sm font-semibold text-base-content hover:underline focus-visible:underline"
              >
                {cardTextPreview(card.text)}
              </a>
            ) : (
              <span className="truncate text-sm font-semibold text-base-content">
                {cardTextPreview(card.text)}
              </span>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="truncate font-mono text-base-content/60">
              {coordinate}
            </span>
            {card.column_title ? (
              <>
                <span className="text-base-content/30">·</span>
                <span className="text-base-content/60">
                  {card.column_title}
                </span>
              </>
            ) : null}
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span
              data-testid={`card-availability-${card.id}`}
              data-availability={card.availability}
              className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 font-semibold"
              style={{
                backgroundColor: availability.surface,
                color: availability.ink
              }}
            >
              {availability.label}
            </span>
            {archived ? (
              <span
                data-testid={`card-archived-${card.id}`}
                className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 font-semibold"
                style={{
                  backgroundColor: "var(--color-status-attention-surface)",
                  color: "var(--color-status-attention-ink)"
                }}
              >
                <Archive aria-hidden size={12} strokeWidth={2.25} />В архиве
              </span>
            ) : null}
          </div>
        </div>

        <div className="flex flex-shrink-0 items-center gap-2">
          <CardRowMenu label={coordinate}>
            <button
              type="button"
              role="menuitem"
              aria-label={`Обновить ${coordinate}`}
              disabled={busy}
              onClick={() => {
                void refresh();
              }}
              className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-base-content hover:bg-base-200 disabled:opacity-60"
            >
              <RefreshCw
                aria-hidden
                size={14}
                strokeWidth={2}
                className={refreshing ? "animate-spin" : undefined}
              />
              {refreshing ? "Обновляем…" : "Обновить"}
            </button>
            <div role="separator" className="my-1 border-t border-base-300" />
            <button
              type="button"
              role="menuitem"
              aria-label={`Отвязать ${coordinate}`}
              disabled={busy}
              onClick={() => {
                void detach();
              }}
              className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-error hover:bg-error/10 disabled:opacity-60"
            >
              <Trash2 aria-hidden size={14} strokeWidth={2} />
              {detaching ? "Отвязываем…" : "Отвязать"}
            </button>
          </CardRowMenu>
        </div>
      </div>

      <details className="group">
        <summary className="cursor-pointer list-none text-xs font-medium text-base-content/60 hover:text-base-content">
          Текст карточки
        </summary>
        <MarkdownView
          markdown={card.text}
          className="mt-2 max-h-72 overflow-y-auto rounded-md border border-base-200 bg-base-200/40 px-3 py-2 text-sm"
        />
      </details>

      {error ? (
        <p role="alert" className="m-0 text-xs text-error">
          {error}
        </p>
      ) : null}
    </li>
  );
}
