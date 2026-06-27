import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  total: 3
}));

vi.mock("../model/use-part-patch-counts", () => ({
  usePartPatchCounts: () => ({ counts: new Map(), total: mocks.total })
}));

vi.mock("./SystemSlot", () => ({
  SystemSlot: () => <div data-testid="system-slot" />
}));

vi.mock("./SkillsSlot", () => ({
  SkillsSlot: () => <div data-testid="skills-slot" />
}));

import { AgentContextPage } from "./AgentContextPage";

afterEach(() => {
  cleanup();
});

describe("AgentContextPage", () => {
  it("показывает один экран с четырьмя слотами состава", () => {
    render(<AgentContextPage />);

    expect(
      screen.getByRole("heading", { level: 1, name: "Состав агента" })
    ).toBeTruthy();

    for (const title of [
      "System-промпт",
      "User-промпт",
      "Скилы",
      "Скилы юзера"
    ]) {
      expect(screen.getByRole("heading", { name: title })).toBeTruthy();
    }
  });

  it("слот пользовательских скилов помечен «скоро»", () => {
    render(<AgentContextPage />);
    expect(screen.getByText("скоро")).toBeTruthy();
  });

  it("счётчик правок на ревью выводится в слоте System", () => {
    render(<AgentContextPage />);
    expect(screen.getByText("3 на ревью")).toBeTruthy();
  });
});
