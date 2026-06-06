import type { CapabilityName } from "@/entities/capability";

export type PanelPlacement = "primary" | "review" | "context" | "terminal";

export interface PanelGating {
  placement: PanelPlacement;
  order: number;
  capability?: CapabilityName;
}

/**
 * Чистый отбор панелей для региона: фильтр по placement, по capability-гейту,
 * затем сортировка по order. Вынесено из реестра, чтобы композицию панелей
 * можно было покрыть тестом без монтирования виджетов.
 */
export function selectPanels<T extends PanelGating>(
  panels: readonly T[],
  placement: PanelPlacement,
  isCapabilityEnabled: (capability: CapabilityName) => boolean
): T[] {
  return panels
    .filter((panel) => panel.placement === placement)
    .filter(
      (panel) =>
        panel.capability === undefined || isCapabilityEnabled(panel.capability)
    )
    .sort((a, b) => a.order - b.order);
}
