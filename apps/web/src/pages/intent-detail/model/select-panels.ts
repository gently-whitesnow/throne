export type PanelPlacement = "primary" | "review" | "context" | "terminal";

export interface PanelGating {
  placement: PanelPlacement;
  order: number;
}

/**
 * Чистый отбор панелей для региона: фильтр по placement, затем сортировка по
 * order. Вынесено из реестра, чтобы композицию панелей можно было покрыть
 * тестом без монтирования виджетов.
 */
export function selectPanels<T extends PanelGating>(
  panels: readonly T[],
  placement: PanelPlacement
): T[] {
  return panels
    .filter((panel) => panel.placement === placement)
    .sort((a, b) => a.order - b.order);
}
