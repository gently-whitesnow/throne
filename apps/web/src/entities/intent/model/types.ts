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
  "ready_for_work",
  "work",
  "ready_for_review",
  "done",
  "reject"
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
    ink: "#1F9D88",
    surface: "#E7F5ED"
  },
  ready_for_review: {
    label: "Нужно внимание",
    ink: "#A87900",
    surface: "#FFF3D6"
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
  }
};
