import type { IntentsComponents } from "@/shared/api";

export type IntentListItem = IntentsComponents["schemas"]["IntentListItemDto"];

export type IntentDetail = IntentsComponents["schemas"]["IntentDetailDto"];

export type IntentAttachment =
  IntentsComponents["schemas"]["IntentAttachmentDto"];

export type IntentStatus = IntentsComponents["schemas"]["IntentStatus"];

export type IntentLinkPeer = IntentsComponents["schemas"]["IntentLinkPeerDto"];

export type IntentLinksSummaryEntry =
  IntentsComponents["schemas"]["IntentLinksSummaryEntryDto"];

export interface IntentPreview {
  id: string;
  status: IntentStatus;
  title: string;
  summary: string;
  updatedAt: string;
  textVersion: number;
}

export interface IntentStatusMeta {
  label: string;
  ink: string;
  surface: string;
}

export const intentStatusOrder: IntentStatus[] = [
  "draft",
  "interview",
  "ready_for_work",
  "work",
  "awaiting_operator",
  "done",
  "reject",
  "fridge"
];

export const intentStatusMeta: Record<IntentStatus, IntentStatusMeta> = {
  draft: {
    label: "Черновик",
    ink: "var(--color-status-neutral-ink)",
    surface: "var(--color-status-neutral-surface)"
  },
  interview: {
    label: "Интервью",
    ink: "var(--color-status-info-ink)",
    surface: "var(--color-status-info-surface)"
  },
  ready_for_work: {
    label: "Готов к работе",
    ink: "var(--color-status-info-strong-ink)",
    surface: "var(--color-status-info-surface)"
  },
  work: {
    label: "В работе",
    // Purple (design-system `secondary`) so `work` reads apart from the green
    // `done` status instead of a near-identical teal.
    ink: "var(--color-status-progress-ink)",
    surface: "var(--color-status-progress-surface)"
  },
  awaiting_operator: {
    label: "Жду ответа",
    ink: "var(--color-status-attention-ink)",
    surface: "var(--color-status-attention-surface)"
  },
  done: {
    label: "Готово",
    ink: "var(--color-status-success-ink)",
    surface: "var(--color-status-success-surface)"
  },
  reject: {
    label: "Отклонено",
    ink: "var(--color-status-danger-ink)",
    surface: "var(--color-status-danger-surface)"
  },
  fridge: {
    label: "Холодильник",
    ink: "var(--color-status-archive-ink)",
    surface: "var(--color-status-archive-surface)"
  }
};

/**
 * Statuses that signal the operator is expected to act: agent stopped mid-pass
 * (`awaiting_operator`). Drives the cross-context Inbox widget in the sidebar.
 */
export const INBOX_STATUSES: ReadonlySet<IntentStatus> = new Set([
  "awaiting_operator"
]);

export const ARCHIVE_STATUSES: ReadonlySet<IntentStatus> = new Set([
  "done",
  "reject"
]);

export const FRIDGE_STATUS: IntentStatus = "fridge";
