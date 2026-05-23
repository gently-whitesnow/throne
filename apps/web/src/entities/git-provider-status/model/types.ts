import type { SettingsComponents } from "@/shared/api";

export type GitProvidersStatus =
  SettingsComponents["schemas"]["GitProvidersStatusDto"];

export type GitProviderAuthStatus =
  SettingsComponents["schemas"]["GitProviderAuthStatusDto"];

export interface GitProviderHealthMeta {
  label: string;
  ink: string;
  surface: string;
}

/**
 * Light-first semantic tokens for the provider health pill on `/settings`.
 * Light = authenticated, dark amber = misconfigured. Slice 1 ships GitHub only
 * (`gitlab` arrives in slice 5) — the meta map is keyed by health, not provider.
 */
export const gitProviderHealthMeta: Record<
  "ok" | "broken",
  GitProviderHealthMeta
> = {
  ok: {
    label: "Подключено",
    ink: "#1F8F5F",
    surface: "#E7F5ED"
  },
  broken: {
    label: "Нет авторизации",
    ink: "#CF4D4D",
    surface: "#FDEAEA"
  }
};
