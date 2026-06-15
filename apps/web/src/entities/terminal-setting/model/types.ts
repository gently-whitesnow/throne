import type { SettingsComponents, TerminalComponents } from "@/shared/api";

export type TerminalAgentVendor =
  TerminalComponents["schemas"]["TerminalAgentVendor"];

export type TerminalReasoningEffort =
  TerminalComponents["schemas"]["TerminalReasoningEffort"];

export type TerminalSettings =
  SettingsComponents["schemas"]["TerminalSettingsDto"];

/** Метаданные одного вендора, как их отдаёт backend (`GET /terminal/vendors`). */
export type TerminalVendorMetadata =
  TerminalComponents["schemas"]["TerminalVendorMetadataDto"];

/** Каталог вендоров — единый источник правды для surface запуска и настроек. */
export type TerminalVendorCatalog =
  TerminalComponents["schemas"]["TerminalVendorCatalogResponse"];

/** Подписи уровней усилия для UI. Сам список доступных усилий приходит из metadata. */
export const EFFORT_LABEL: Record<TerminalReasoningEffort, string> = {
  low: "Low",
  medium: "Medium",
  high: "High",
  xhigh: "Xhigh"
};

/** Метаданные вендора из каталога; undefined, если вендор не зарегистрирован. */
export function findVendorMetadata(
  catalog: TerminalVendorCatalog | undefined,
  vendor: TerminalAgentVendor
): TerminalVendorMetadata | undefined {
  return catalog?.vendors.find((v) => v.vendor === vendor);
}

/**
 * Вендор по умолчанию для предзаполнения surface запуска: persisted-настройка
 * главнее, иначе — дефолт каталога. Возвращает undefined, пока каталог не загружен.
 */
export function resolveDefaultVendor(
  catalog: TerminalVendorCatalog | undefined,
  settingsVendor: TerminalAgentVendor | undefined
): TerminalAgentVendor | undefined {
  if (catalog === undefined) return undefined;
  if (
    settingsVendor !== undefined &&
    catalog.vendors.some((v) => v.vendor === settingsVendor)
  ) {
    return settingsVendor;
  }
  return catalog.default_vendor;
}
