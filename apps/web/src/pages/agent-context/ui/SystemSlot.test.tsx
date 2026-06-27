import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/widgets/prompt-parts-board", () => ({
  PromptPartsBoard: (props: {
    patchCounts?: Map<string, number>;
    onShowPatches?: (part: { scope: string; key: string }) => void;
  }) => (
    <div data-testid="board">
      <span>board:{props.patchCounts?.get("user/commit") ?? 0}</span>
      <button
        type="button"
        onClick={() => {
          props.onShowPatches?.({ scope: "user", key: "commit" });
        }}
      >
        show-commit-patches
      </button>
    </div>
  )
}));

vi.mock("@/widgets/improvements-patches-tab", () => ({
  PatchesTab: (props: { targetScope?: string; targetKey?: string }) => (
    <div data-testid="patches">
      patches:{props.targetScope ?? "-"}/{props.targetKey ?? "-"}
    </div>
  )
}));

vi.mock("@/widgets/improvements-dreams-tab", () => ({
  DreamsTab: () => <div data-testid="dreams">dreams</div>
}));

import { SystemSlot } from "./SystemSlot";

const renderSlot = () =>
  render(
    <SystemSlot patchCounts={new Map([["user/commit", 2]])} proposedTotal={2} />
  );

afterEach(() => {
  cleanup();
});

describe("SystemSlot", () => {
  it("по умолчанию показывает части и пробрасывает patchCounts", () => {
    renderSlot();
    expect(screen.getByText("board:2")).toBeTruthy();
    expect(screen.queryByTestId("patches")).toBeNull();
  });

  it("«N правок» части открывает её улучшения с фильтром по части", () => {
    renderSlot();
    fireEvent.click(screen.getByText("show-commit-patches"));

    expect(screen.getByTestId("patches").textContent).toContain(
      "patches:user/commit"
    );
    expect(screen.queryByTestId("board")).toBeNull();
  });

  it("прямой клик по вкладке «Улучшения» сбрасывает фильтр части", () => {
    renderSlot();
    fireEvent.click(screen.getByText("show-commit-patches"));
    fireEvent.click(screen.getByRole("tab", { name: /Улучшения/ }));

    expect(screen.getByTestId("patches").textContent).toContain("patches:-/-");
  });

  it("вкладка «История» показывает dreams", () => {
    renderSlot();
    fireEvent.click(screen.getByRole("tab", { name: "История" }));
    expect(screen.getByTestId("dreams")).toBeTruthy();
  });
});
