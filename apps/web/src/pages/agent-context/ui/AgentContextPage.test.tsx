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

import { AgentContextPage } from "./AgentContextPage";

afterEach(() => {
  cleanup();
});

describe("AgentContextPage", () => {
  it("показывает единственный редактируемый блок — system-промпт", () => {
    render(<AgentContextPage />);

    expect(
      screen.getByRole("heading", { level: 1, name: "System-промпт" })
    ).toBeTruthy();
    expect(screen.getByTestId("system-slot")).toBeTruthy();
  });

  it("скрытые слоты состава не выводятся", () => {
    render(<AgentContextPage />);

    for (const title of [
      "User-промпт",
      "Throne-скилы",
      "Пользовательские скилы"
    ]) {
      expect(screen.queryByRole("heading", { name: title })).toBeNull();
    }
    expect(screen.queryByText("скоро")).toBeNull();
  });

  it("счётчик правок на ревью выводится рядом с заголовком", () => {
    render(<AgentContextPage />);
    expect(screen.getByText("3 на ревью")).toBeTruthy();
  });
});
