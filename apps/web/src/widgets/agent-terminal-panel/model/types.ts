import type { TerminalComponents } from "@/shared/api";

export type TerminalRunMode = TerminalComponents["schemas"]["TerminalRunMode"];

export type TerminalSessionState =
  TerminalComponents["schemas"]["TerminalSessionState"];

export type RunIntentTerminalResponse =
  TerminalComponents["schemas"]["RunIntentTerminalResponse"];

// Dream намеренно отсутствует: dream-режим запускается вне контекста интента,
// поэтому в панели запуска агента на странице интента он не предлагается.
export const TERMINAL_RUN_MODES: readonly TerminalRunMode[] = [
  "interview",
  "work"
] as const;

export const RUN_MODE_LABEL: Record<TerminalRunMode, string> = {
  work: "Работа",
  interview: "Интервью",
  dream: "Dream"
};
