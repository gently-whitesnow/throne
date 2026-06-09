import type {
  TerminalAgentVendor,
  TerminalReasoningEffort
} from "@/entities/terminal-setting";

import type { TerminalRunMode } from "./types";

/**
 * Загвоздка load-bearing: формулировка «Прочитай бандл … и … интент …» —
 * единственная фраза, на которой MCP MiniRouter уверенно выбирает нужный
 * bundle (см. Slice 2 «Решения интервью v2 → Q8» и memory
 * `feedback_throne_bundle_prompt`). Override не допускается.
 */
const MODE_VERB: Record<TerminalRunMode, string> = {
  work: "выполни",
  interview: "проведи интервью по",
  dream: "проведи дрим по",
  free: ""
};

/** Заглушка, которую оператор стирает и дописывает своим вопросом в free-режиме. */
export const FREE_QUESTION_PLACEHOLDER = "<твой вопрос>";

export function buildAgentPrompt(
  mode: TerminalRunMode,
  intentId: string
): string {
  // free не читает бандл — фраза намеренно без «бандл», чтобы MiniRouter не цеплялся
  // за режим; оператор сам дописывает вопрос вместо плейсхолдера.
  if (mode === "free") {
    return `прочитай интент ${intentId} и ${FREE_QUESTION_PLACEHOLDER}`;
  }
  return `Прочитай бандл ${mode} и ${MODE_VERB[mode]} интент ${intentId}`;
}

/**
 * Команда ручного запуска для кнопки «Copy prompt». Зеркалит бэкендовый
 * `AgentSpawnCommand`: одинаковая ось вендор/модель/усилие, чтобы скопированная
 * команда совпадала с тем, что Throne спавнит в tmux.
 */
export function buildAgentCliCommand(
  vendor: TerminalAgentVendor,
  model: string,
  effort: TerminalReasoningEffort,
  prompt: string
): string {
  const escaped = prompt.replace(/"/g, '\\"');
  const flags =
    vendor === "codex"
      ? `-m ${model} -c model_reasoning_effort=${effort}`
      : `--model ${model} --effort ${effort}`;
  return `${vendor} ${flags} "${escaped}"`;
}
