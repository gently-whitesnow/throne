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
  "ready_for_review",
  "awaiting_operator",
  "done",
  "reject",
  "fridge"
];

export const intentStatusMeta: Record<IntentStatus, IntentStatusMeta> = {
  draft: {
    label: "Черновик",
    ink: "#4C5567",
    surface: "#F6F7FB"
  },
  interview: {
    label: "Интервью",
    ink: "#3C78F2",
    surface: "#E8F0FF"
  },
  ready_for_work: {
    label: "Готов к работе",
    ink: "#274DC6",
    surface: "#E8F0FF"
  },
  work: {
    label: "В работе",
    // Purple (design-system `secondary` oklch(0.48 0.14 280)) so `work` reads
    // apart from the green `done` status instead of a near-identical teal.
    ink: "#5351AA",
    surface: "#ECEAFB"
  },
  ready_for_review: {
    label: "Жду ревью",
    ink: "#A87900",
    surface: "#FFF3D6"
  },
  awaiting_operator: {
    label: "Жду ответа",
    ink: "#B14E1A",
    surface: "#FDE6D6"
  },
  done: {
    label: "Готово",
    ink: "#1F8F5F",
    surface: "#E7F5ED"
  },
  reject: {
    label: "Отклонено",
    ink: "#CF4D4D",
    surface: "#FDEAEA"
  },
  fridge: {
    label: "Холодильник",
    ink: "#4F6F94",
    surface: "#E8EEF7"
  }
};

/**
 * Statuses that signal the operator is expected to act:
 * agent finished the pass (`ready_for_review`) or stopped mid-pass (`awaiting_operator`).
 * Drives the cross-context Inbox widget in the sidebar.
 */
export const INBOX_STATUSES: ReadonlySet<IntentStatus> = new Set([
  "ready_for_review",
  "awaiting_operator"
]);

export const ARCHIVE_STATUSES: ReadonlySet<IntentStatus> = new Set([
  "done",
  "reject"
]);

export const FRIDGE_STATUS: IntentStatus = "fridge";
