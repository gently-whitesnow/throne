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
  dream: "проведи дрим по"
};

export function buildAgentPrompt(
  mode: TerminalRunMode,
  intentId: string
): string {
  return `Прочитай бандл ${mode} и ${MODE_VERB[mode]} интент ${intentId}`;
}

export function buildClaudeCliCommand(prompt: string): string {
  const escaped = prompt.replace(/"/g, '\\"');
  return `claude "${escaped}"`;
}
