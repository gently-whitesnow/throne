import type { CapabilitiesComponents, TerminalComponents } from "@/shared/api";

export type CapabilityDto = CapabilitiesComponents["schemas"]["CapabilityDto"];

export type CapabilityName =
  CapabilitiesComponents["schemas"]["CapabilityName"];

export type TerminalRunMode = TerminalComponents["schemas"]["TerminalRunMode"];

export type TerminalSessionState =
  TerminalComponents["schemas"]["TerminalSessionState"];

export type RunIntentTerminalResponse =
  TerminalComponents["schemas"]["RunIntentTerminalResponse"];

export const TERMINAL_RUN_MODES: readonly TerminalRunMode[] = [
  "work",
  "interview",
  "dream"
] as const;

export const RUN_MODE_LABEL: Record<TerminalRunMode, string> = {
  work: "Работа",
  interview: "Интервью",
  dream: "Dream"
};
