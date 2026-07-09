import type {
  CardAttachmentsComponents,
  TaskTrackersComponents
} from "@/shared/api";

export type CardAttachment =
  CardAttachmentsComponents["schemas"]["CardAttachmentDto"];

/**
 * Provider-neutral, read-only projection of an external card as the board card
 * browser sees it (ADR-0052). Never an intent, never written back upstream. List
 * rows omit `description` — the single-card GET fills it in.
 */
export type TaskTrackerCard =
  TaskTrackersComponents["schemas"]["TaskTrackerCardDto"];

export type TaskTrackerBoardCardsResponse =
  TaskTrackersComponents["schemas"]["TaskTrackerBoardCardsResponse"];

export type CardAvailability =
  CardAttachmentsComponents["schemas"]["CardAvailability"];

export type AttachCardRequest =
  CardAttachmentsComponents["schemas"]["AttachCardRequest"];

export type TaskTrackerProvider =
  CardAttachmentsComponents["schemas"]["TaskTrackerProvider"];

export interface CardAvailabilityMeta {
  label: string;
  ink: string;
  surface: string;
}

/**
 * Трёхстатусный индикатор доступности снапшота (ADR-0052). Цветовая конвенция
 * донора (`entities/git-provider-status` / `taskTrackerStateMeta`):
 * `available` — нейтральный (снапшот свежий), `unavailable` — янтарь (трекер был
 * недостижим на последнем refresh, показываем прошлый снапшот), `gone` — красный
 * (карточка исчезла/закрыта upstream). На чтении не переопрашивается.
 */
export const cardAvailabilityMeta: Record<
  CardAvailability,
  CardAvailabilityMeta
> = {
  available: {
    label: "Доступна",
    ink: "var(--color-status-neutral-ink)",
    surface: "var(--color-status-neutral-surface)"
  },
  unavailable: {
    label: "Трекер недоступен",
    ink: "var(--color-status-attention-ink)",
    surface: "var(--color-status-attention-surface)"
  },
  gone: {
    label: "Карточка недоступна",
    ink: "var(--color-status-danger-ink)",
    surface: "var(--color-status-danger-surface)"
  }
};
