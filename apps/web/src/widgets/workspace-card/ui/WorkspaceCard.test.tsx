import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { WorkspaceCard } from "./WorkspaceCard";

const fetchWorkspaceSettings = vi.fn<() => Promise<unknown>>();

// Хук берёт API из относительного пути; мокаем именно его,
// чтобы публичный barrel продолжал отдавать настоящие селекторы.
vi.mock("@/entities/workspace-setting/api/workspace-settings-api", () => ({
  fetchWorkspaceSettings: () => fetchWorkspaceSettings()
}));

describe("WorkspaceCard", () => {
  beforeEach(() => {
    fetchWorkspaceSettings.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает «Загружаем параметры…» до первого ответа", async () => {
    let resolve!: (v: unknown) => void;
    fetchWorkspaceSettings.mockReturnValue(
      new Promise((r) => {
        resolve = r;
      })
    );

    render(<WorkspaceCard />);

    expect(screen.getByText(/Загружаем параметры/)).toBeTruthy();

    resolve({
      root: "/home/u/.throne/workspaces",
      total_size_bytes: 0,
      status: "ready"
    });

    await waitFor(() => {
      expect(screen.queryByText(/Загружаем параметры/)).toBeNull();
    });
  });

  it("рендерит путь моноширинно и форматирует размер при status=ready", async () => {
    fetchWorkspaceSettings.mockResolvedValue({
      root: "/Users/octocat/.throne/workspaces",
      // 1.5 MiB
      total_size_bytes: 1024 * 1024 + 512 * 1024,
      status: "ready"
    });

    render(<WorkspaceCard />);

    await waitFor(() => {
      expect(
        screen.getByText("/Users/octocat/.throne/workspaces")
      ).toBeTruthy();
    });

    const pathCell = screen.getByText("/Users/octocat/.throne/workspaces");
    expect(pathCell.className).toMatch(/font-mono/);

    const sizeCell = screen.getByTestId("workspace-size");
    expect(sizeCell.getAttribute("data-status")).toBe("ready");
    expect(sizeCell.textContent).toMatch(/1\.5 MiB/);
  });

  it("показывает индикатор «Считаем размер…» при status=calculating", async () => {
    fetchWorkspaceSettings.mockResolvedValue({
      root: "/home/u/.throne/workspaces",
      status: "calculating"
    });

    render(<WorkspaceCard />);

    await waitFor(() => {
      expect(screen.getByText(/Считаем размер/)).toBeTruthy();
    });

    const sizeCell = screen.getByTestId("workspace-size");
    expect(sizeCell.getAttribute("data-status")).toBe("calculating");
  });

  it("опрашивает повторно, пока статус не станет ready", async () => {
    fetchWorkspaceSettings
      .mockResolvedValueOnce({
        root: "/ws",
        status: "calculating"
      })
      .mockResolvedValueOnce({
        root: "/ws",
        total_size_bytes: 2048,
        status: "ready"
      });

    vi.useFakeTimers();
    try {
      render(<WorkspaceCard />);

      // Дождаться первого ответа (calculating) — промисы прокручиваем сами,
      // потому что fake timers замораживают macrotask-очередь.
      await act(async () => {
        await Promise.resolve();
        await Promise.resolve();
      });

      expect(fetchWorkspaceSettings).toHaveBeenCalledTimes(1);

      // Прокрутить таймер polling-а и дать промисам разрешиться.
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2_500);
      });

      expect(fetchWorkspaceSettings).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }

    await waitFor(() => {
      expect(screen.queryByText(/Считаем размер/)).toBeNull();
    });
  });

  it("повторно дёргает API по клику «Обновить»", async () => {
    fetchWorkspaceSettings.mockResolvedValue({
      root: "/ws",
      total_size_bytes: 0,
      status: "ready"
    });

    render(<WorkspaceCard />);

    await waitFor(() => {
      expect(fetchWorkspaceSettings).toHaveBeenCalledTimes(1);
    });

    fireEvent.click(screen.getByRole("button", { name: /Перепроверить/ }));

    await waitFor(() => {
      expect(fetchWorkspaceSettings).toHaveBeenCalledTimes(2);
    });
  });

  it("показывает ошибку, если запрос упал", async () => {
    fetchWorkspaceSettings.mockRejectedValue(new Error("boom"));

    render(<WorkspaceCard />);

    await waitFor(() => {
      expect(screen.getByRole("alert").textContent).toMatch(/boom/);
    });
  });
});
