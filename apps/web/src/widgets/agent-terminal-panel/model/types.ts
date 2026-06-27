import type { IntentStatus } from "@/entities/intent";
import type {
  TerminalAgentVendor,
  TerminalReasoningEffort
} from "@/entities/terminal-setting";
import type { TerminalComponents } from "@/shared/api";

export type TerminalRunMode = TerminalComponents["schemas"]["TerminalRunMode"];

/**
 * Полная ось запуска одной сессии: режим + вендор/модель/усилие. `effort` равен
 * null для вендора без оси reasoning effort (`supports_effort=false`) — тогда флаг
 * усилия не доходит до spawn-argv (бэкенд резолвит его в null).
 */
export interface TerminalLaunchArgs {
  mode: TerminalRunMode;
  vendor: TerminalAgentVendor;
  model: string;
  effort: TerminalReasoningEffort | null;
}

export type TerminalSessionState =
  TerminalComponents["schemas"]["TerminalSessionState"];

export type RunIntentTerminalResponse =
  TerminalComponents["schemas"]["RunIntentTerminalResponse"];

/**
 * Persisted launch axis of an intent returned by the run response and the status probe
 * (ADR-0041). With a live session these are the running session's real parameters; otherwise the
 * intent's last-used choice the controls pre-fill from. Distinct from {@link TerminalLaunchArgs},
 * which is the front-assembled axis for an outgoing launch.
 */
export type PersistedLaunchArgs =
  TerminalComponents["schemas"]["TerminalLaunchArgs"];

export type IntentTerminalPreviewResponse =
  TerminalComponents["schemas"]["IntentTerminalPreviewResponse"];

export type PromptPartPreview =
  TerminalComponents["schemas"]["PromptPartPreviewDto"];

export type AvailableSessionSkill =
  TerminalComponents["schemas"]["AvailableSessionSkillDto"];

export type AttachIntentTerminalSkillsResponse =
  TerminalComponents["schemas"]["AttachIntentTerminalSkillsResponse"];

export type OpenNativeTerminalResponse =
  TerminalComponents["schemas"]["OpenNativeTerminalResponse"];

export type IntentTextUpdate =
  TerminalComponents["schemas"]["IntentTextUpdate"];

/**
 * Полезная нагрузка одного запуска из preflight-модалки: ось запуска плюс
 * собранный контекст (выбранные части, итоговые system/user промпты) и
 * опциональное сохранение тела интента. Backend берёт system/user verbatim —
 * фронт runtime-промпт не пересобирает (ADR-0030/0035).
 */
export interface TerminalRunPayload {
  launch: TerminalLaunchArgs;
  reviewBindingId: string | null;
  selectedPartIds: string[];
  selectedSkillIds: string[];
  systemPrompt: string;
  userPrompt: string;
  intentTextUpdate: IntentTextUpdate | null;
  /**
   * Геометрия встроенного терминала, измеренная фронтом перед спавном. Сервер стартует
   * tmux-сессию в ней (new-session -x/-y), чтобы первый кадр агента сразу совпал с клиентом
   * и начальный resize не вызывал reflow. Отсутствует → дефолт 80×24 (один reflow на attach).
   */
  viewport?: { cols: number; rows: number } | null;
}

// Dream намеренно отсутствует: dream-режим запускается вне контекста интента,
// поэтому в панели запуска агента на странице интента он не предлагается.
// free доступен в любом статусе — оператор сам формулирует задачу терминалу.
export const TERMINAL_RUN_MODES: readonly TerminalRunMode[] = [
  "interview",
  "work",
  "review",
  "free"
] as const;

export const RUN_MODE_LABEL: Record<TerminalRunMode, string> = {
  work: "Работа",
  interview: "Интервью",
  review: "Review",
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
