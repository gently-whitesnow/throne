import type { SettingsComponents } from "@/shared/api";

export type GitProvidersStatus =
  SettingsComponents["schemas"]["GitProvidersStatusDto"];

export type GitProviderAuthStatus =
  SettingsComponents["schemas"]["GitProviderAuthStatusDto"];

export type GitProviderStatusEntry =
  SettingsComponents["schemas"]["GitProviderStatusDto"];

export interface GitProviderHealthMeta {
  label: string;
  className: string;
}

/**
 * Light-first semantic tokens for the provider health pill on `/settings`.
 */
export const gitProviderHealthMeta: Record<
  "ok" | "offline" | "broken" | "missing",
  GitProviderHealthMeta
> = {
  ok: {
    label: "Подключено",
    className: "bg-success/10 text-success"
  },
  offline: {
    label: "Вне сети",
    className: "bg-warning/20 text-warning"
  },
  broken: {
    label: "Нет авторизации",
    className: "bg-error/10 text-error"
  },
  missing: {
    label: "Не настроено",
    className: "bg-error/10 text-error"
  }
};
