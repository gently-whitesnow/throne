import { describe, expect, it } from "vitest";

import { buildAgentCliCommand, buildAgentPrompt } from "./prompt";

describe("buildAgentPrompt", () => {
  // Эти строки load-bearing: MCP MiniRouter ловит их и переключает bundle.
  // См. memory `feedback_throne_bundle_prompt` и Slice 2 «Решения интервью v2 → Q8».
  it("формирует фразу с глаголом «выполни» для режима work", () => {
    expect(buildAgentPrompt("work", "abc123")).toBe(
      "Прочитай бандл work и выполни интент abc123"
    );
  });

  it("формирует фразу «проведи интервью по» для режима interview", () => {
    expect(buildAgentPrompt("interview", "abc123")).toBe(
      "Прочитай бандл interview и проведи интервью по интент abc123"
    );
  });

  it("формирует фразу «проведи дрим по» для режима dream", () => {
    expect(buildAgentPrompt("dream", "abc123")).toBe(
      "Прочитай бандл dream и проведи дрим по интент abc123"
    );
  });

  it("в режиме free не читает бандл и оставляет плейсхолдер вопроса", () => {
    expect(buildAgentPrompt("free", "abc123")).toBe(
      "прочитай интент abc123 и <твой вопрос>"
    );
  });
});

describe("buildAgentCliCommand", () => {
  it("claude: --model/--effort с обёрнутым промптом", () => {
    expect(buildAgentCliCommand("claude", "opus", "high", "test prompt")).toBe(
      'claude --model opus --effort high "test prompt"'
    );
  });

  it("codex: -m и -c model_reasoning_effort без shell-кавычек", () => {
    expect(
      buildAgentCliCommand("codex", "gpt-5.5", "medium", "test prompt")
    ).toBe('codex -m gpt-5.5 -c model_reasoning_effort=medium "test prompt"');
  });

  it("экранирует двойные кавычки внутри промпта", () => {
    expect(buildAgentCliCommand("claude", "opus", "high", 'say "hi"')).toBe(
      'claude --model opus --effort high "say \\"hi\\""'
    );
  });
});
