import type { DreamComponents } from "@/shared/api";

export type DreamReadiness = DreamComponents["schemas"]["DreamReadinessDto"];
export type DreamReadinessStatus =
  DreamComponents["schemas"]["DreamReadinessStatus"];

export interface ReadinessStatusMeta {
  label: string;
  ink: string;
  surface: string;
  description: string;
}

export const readinessStatusMeta: Record<
  DreamReadinessStatus,
  ReadinessStatusMeta
> = {
  empty: {
    label: "Empty",
    ink: "#4C5567",
    surface: "#F6F7FB",
    description: "Нет qa/review в окне — добавьте обратной связи."
  },
  has_content: {
    label: "Has content",
    ink: "#1F8F5F",
    surface: "#E7F5ED",
    description: "Можно запускать /dream."
  },
  pending_review: {
    label: "Review pending",
    ink: "#A87900",
    surface: "#FFF3D6",
    description: "Есть незакрытые dream-run'ы — сначала разберитесь с ними."
  }
};
