import { describe, expect, it } from "vitest";

import { buildAgentPrompt, buildClaudeCliCommand } from "./prompt";

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

  it("формирует фразу «проведи dream-проход по» для режима dream", () => {
    expect(buildAgentPrompt("dream", "abc123")).toBe(
      "Прочитай бандл dream и проведи dream-проход по интент abc123"
    );
  });
});

describe("buildClaudeCliCommand", () => {
  it("оборачивает промпт в `claude \"…\"`", () => {
    expect(buildClaudeCliCommand("test prompt")).toBe('claude "test prompt"');
  });

  it("экранирует двойные кавычки внутри промпта", () => {
    expect(buildClaudeCliCommand('say "hi"')).toBe('claude "say \\"hi\\""');
  });
});
