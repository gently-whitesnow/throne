import type { SettingsComponents } from "@/shared/api";

export type LocalModelCatalog =
  SettingsComponents["schemas"]["LocalModelCatalogDto"];

export type LocalModelDiscoveryStatus =
  SettingsComponents["schemas"]["LocalModelDiscoveryStatus"];

export interface LocalModelStatusMeta {
  label: string;
  className: string;
}

/**
 * Light-first semantic tokens for the local-model discovery pill on `/settings`.
 * `ready` — success; `unreachable` — error; `not_configured` / `empty` — neutral warning.
 */
export const localModelStatusMeta: Record<
  LocalModelDiscoveryStatus,
  LocalModelStatusMeta
> = {
  ready: {
    label: "Подключено",
    className: "bg-success/10 text-success"
  },
  empty: {
    label: "Нет моделей",
    className: "bg-warning/20 text-warning"
  },
  not_configured: {
    label: "Не настроено",
    className: "bg-base-300 text-base-content/70"
  },
  unreachable: {
    label: "Недоступен",
    className: "bg-error/10 text-error"
  }
};
