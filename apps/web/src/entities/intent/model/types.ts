import type { IntentsComponents } from "@/shared/api";

export type IntentListItem = IntentsComponents["schemas"]["IntentListItemDto"];

export type IntentDetail = IntentsComponents["schemas"]["IntentDetailDto"];

export type IntentStatus = "draft" | "active" | "review";

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

export const intentStatusMeta: Record<IntentStatus, IntentStatusMeta> = {
  draft: {
    label: "Черновик",
    ink: "#600000",
    surface: "#ffc6c6"
  },
  active: {
    label: "В работе",
    ink: "#187574",
    surface: "#c3faf5"
  },
  review: {
    label: "Проверка",
    ink: "#746019",
    surface: "#ffe6cd"
  }
};
