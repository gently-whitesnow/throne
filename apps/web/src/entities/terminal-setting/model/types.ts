import type { SettingsComponents, TerminalComponents } from "@/shared/api";

export type TerminalAgentVendor =
  TerminalComponents["schemas"]["TerminalAgentVendor"];

export type TerminalReasoningEffort =
  TerminalComponents["schemas"]["TerminalReasoningEffort"];

export type TerminalSettings =
  SettingsComponents["schemas"]["TerminalSettingsDto"];

export const TERMINAL_VENDORS: readonly TerminalAgentVendor[] = [
  "claude",
  "codex"
] as const;

export const TERMINAL_EFFORTS: readonly TerminalReasoningEffort[] = [
  "low",
  "medium",
  "high",
  "xhigh"
] as const;

export const VENDOR_LABEL: Record<TerminalAgentVendor, string> = {
  claude: "Claude",
  codex: "Codex"
};

export const EFFORT_LABEL: Record<TerminalReasoningEffort, string> = {
  low: "Low",
  medium: "Medium",
  high: "High",
  xhigh: "Xhigh"
};

/**
 * Курируемые списки моделей по вендору. Хардкод: обновляются правкой кода,
 * свободного ввода модели на surface запуска нет. Зеркалит бэкендовый
 * `TerminalAgentCatalog` — держать в синхроне.
 */
export const VENDOR_MODELS: Record<TerminalAgentVendor, readonly string[]> = {
  claude: ["opus", "sonnet", "haiku"],
  codex: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"]
};

/** Нативный дефолт модели вендора — первый элемент курируемого списка. */
export const VENDOR_DEFAULT_MODEL: Record<TerminalAgentVendor, string> = {
  claude: VENDOR_MODELS.claude[0],
  codex: VENDOR_MODELS.codex[0]
};

/** Нативный дефолт усилия: claude по умолчанию high, codex — medium. */
export const VENDOR_DEFAULT_EFFORT: Record<
  TerminalAgentVendor,
  TerminalReasoningEffort
> = {
  claude: "high",
  codex: "medium"
};

/**
 * Поддерживает ли вендор ось reasoning effort. Зеркалит бэкендовый descriptor
 * (`TerminalVendorDescriptor.SupportsEffort`). Вендор без оси усилия скрывает
 * соответствующий контрол запуска. Оба текущих вендора усилие поддерживают.
 */
export const VENDOR_SUPPORTS_EFFORT: Record<TerminalAgentVendor, boolean> = {
  claude: true,
  codex: true
};

export const DEFAULT_TERMINAL_VENDOR: TerminalAgentVendor = "claude";
