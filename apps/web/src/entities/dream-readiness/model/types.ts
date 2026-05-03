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
    description: "Накапливаем сигналы — обратной связи в окне нет."
  },
  warming_up: {
    label: "Warming up",
    ink: "#3C78F2",
    surface: "#E8F0FF",
    description: "Сигналы поступают, но порога ещё не достигли."
  },
  ready: {
    label: "Ready",
    ink: "#1F8F5F",
    surface: "#E7F5ED",
    description: "Достаточно материала для запуска /tdream."
  },
  rich: {
    label: "Rich",
    ink: "#1F9D88",
    surface: "#E7F5ED",
    description: "Накопилось много сигналов — самое время запустить /tdream."
  },
  pending_review: {
    label: "Review pending",
    ink: "#A87900",
    surface: "#FFF3D6",
    description: "Есть незакрытые dream-run'ы — сначала разберитесь с ними."
  }
};
