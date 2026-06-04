import type { IntentStatus } from "@/entities/intent";
import type { TerminalComponents } from "@/shared/api";

export type TerminalRunMode = TerminalComponents["schemas"]["TerminalRunMode"];

export type TerminalSessionState =
  TerminalComponents["schemas"]["TerminalSessionState"];

export type RunIntentTerminalResponse =
  TerminalComponents["schemas"]["RunIntentTerminalResponse"];

// Dream намеренно отсутствует: dream-режим запускается вне контекста интента,
// поэтому в панели запуска агента на странице интента он не предлагается.
// free доступен в любом статусе — оператор сам формулирует задачу терминалу.
export const TERMINAL_RUN_MODES: readonly TerminalRunMode[] = [
  "interview",
  "work",
  "free"
] as const;

export const RUN_MODE_LABEL: Record<TerminalRunMode, string> = {
  work: "Работа",
  interview: "Интервью",
  dream: "Dream",
  free: "Свободный"
};

/**
 * Дефолтный режим панели запуска зависит от статуса интента: черновик ещё нужно
 * уточнять (интервью), готовый к работе — исполнять (работа), всё остальное оператор
 * чаще запускает свободным вопросом.
 */
export function defaultRunModeForStatus(status: IntentStatus): TerminalRunMode {
  if (status === "draft") return "interview";
  if (status === "ready_for_work") return "work";
  return "free";
}
