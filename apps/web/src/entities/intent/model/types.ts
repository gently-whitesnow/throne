import type { IntentsComponents } from "@/shared/api";

export type IntentListItem = IntentsComponents["schemas"]["IntentListItemDto"];

export type IntentDetail = IntentsComponents["schemas"]["IntentDetailDto"];

export type IntentAttachment =
  IntentsComponents["schemas"]["IntentAttachmentDto"];

export type IntentStatus = IntentsComponents["schemas"]["IntentStatus"];

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
  "work",
  "ready_for_review",
  "done",
  "reject"
];

export const intentStatusMeta: Record<IntentStatus, IntentStatusMeta> = {
  draft: {
    label: "Черновик",
    ink: "#600000",
    surface: "#ffc6c6"
  },
  interview: {
    label: "Интервью",
    ink: "#7b4e00",
    surface: "#ffe6cd"
  },
  work: {
    label: "В работе",
    ink: "#187574",
    surface: "#c3faf5"
  },
  ready_for_review: {
    label: "Нужно внимание",
    ink: "#746019",
    surface: "#f8efb8"
  },
  done: {
    label: "Готово",
    ink: "#0f5a2f",
    surface: "#d8f5e8"
  },
  reject: {
    label: "Отклонено",
    ink: "#7a1123",
    surface: "#fbd4d4"
  }
};
