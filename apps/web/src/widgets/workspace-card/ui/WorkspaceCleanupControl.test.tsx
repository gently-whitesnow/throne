import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { WorkspaceCleanupControl } from "./WorkspaceCleanupControl";

const cleanWorkspace =
  vi.fn<(req: { mode: string; dry_run?: boolean }) => Promise<unknown>>();

vi.mock("@/entities/workspace-setting/api/workspace-settings-api", () => ({
  fetchWorkspaceSettings: vi.fn(),
  cleanWorkspace: (req: { mode: string; dry_run?: boolean }) =>
    cleanWorkspace(req)
}));

const render = () =>
  renderWithQuery(<WorkspaceCleanupControl />, { withBridge: false });

describe("WorkspaceCleanupControl", () => {
  beforeEach(() => {
    cleanWorkspace.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("делает dry-run, спрашивает подтверждение и выполняет очистку", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    cleanWorkspace
      .mockResolvedValueOnce({
        removed_clones: 2,
        freed_bytes: 2048,
        dry_run: true
      })
      .mockResolvedValueOnce({
        removed_clones: 2,
        freed_bytes: 2048,
        dry_run: false
      });

    render();
    fireEvent.click(screen.getByTestId("workspace-clean-closed"));

    await waitFor(() => {
      expect(screen.getByTestId("workspace-clean-result").textContent).toMatch(
        /Удалено клонов: 2/
      );
    });

    expect(cleanWorkspace).toHaveBeenNthCalledWith(1, {
      mode: "closed_only",
      dry_run: true
    });
    expect(cleanWorkspace).toHaveBeenNthCalledWith(2, {
      mode: "closed_only",
      dry_run: false
    });
  });

  it("не выполняет очистку, если подтверждение отклонено", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    cleanWorkspace.mockResolvedValueOnce({
      removed_clones: 5,
      freed_bytes: 4096,
      dry_run: true
    });

    render();
    fireEvent.click(screen.getByTestId("workspace-clean-all"));

    await waitFor(() => {
      expect(cleanWorkspace).toHaveBeenCalledTimes(1);
    });
    expect(window.confirm).toHaveBeenCalledOnce();
    expect(cleanWorkspace).not.toHaveBeenCalledWith({
      mode: "all",
      dry_run: false
    });
  });

  it("сообщает, что чистить нечего, когда dry-run вернул ноль клонов", async () => {
    const confirmSpy = vi.spyOn(window, "confirm");
    cleanWorkspace.mockResolvedValueOnce({
      removed_clones: 0,
      freed_bytes: 0,
      dry_run: true
    });

    render();
    fireEvent.click(screen.getByTestId("workspace-clean-closed"));

    await waitFor(() => {
      expect(screen.getByTestId("workspace-clean-result").textContent).toMatch(
        /Нечего удалять/
      );
    });
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(cleanWorkspace).toHaveBeenCalledTimes(1);
  });
});
