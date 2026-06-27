import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";

interface RuntimeInstanceState {
  isEphemeral: boolean;
  isLoading: boolean;
  isStopping: boolean;
  stop: () => void;
}

const mocks = vi.hoisted(() => ({
  useProposedPatchesCount: vi.fn<() => number>(),
  useRuntimeInstance: vi.fn<() => RuntimeInstanceState>()
}));

vi.mock("@/features/throne-readiness", () => ({
  useThroneReadiness: () => ({ ready: true, isLoading: false })
}));

vi.mock("../model/use-proposed-patches-count", () => ({
  useProposedPatchesCount: () => mocks.useProposedPatchesCount()
}));

vi.mock("../model/use-runtime-instance", () => ({
  useRuntimeInstance: () => mocks.useRuntimeInstance()
}));

import { AppShell } from "./AppShell";

function renderShell() {
  return render(
    <MemoryRouter>
      <Routes>
        <Route path="/" element={<AppShell />}>
          <Route index element={<div>content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe("AppShell", () => {
  beforeEach(() => {
    mocks.useProposedPatchesCount.mockReturnValue(0);
    mocks.useRuntimeInstance.mockReturnValue({
      isEphemeral: false,
      isLoading: false,
      isStopping: false,
      stop: vi.fn()
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("не показывает Завершение для обычного инстанса", () => {
    renderShell();

    expect(screen.queryByLabelText("Завершение")).toBeNull();
  });

  it("показывает Завершение для тестового инстанса и вызывает stop", () => {
    const stop = vi.fn();
    mocks.useRuntimeInstance.mockReturnValue({
      isEphemeral: true,
      isLoading: false,
      isStopping: false,
      stop
    });

    renderShell();
    fireEvent.click(screen.getByLabelText("Завершение"));

    expect(stop).toHaveBeenCalledTimes(1);
  });
});
