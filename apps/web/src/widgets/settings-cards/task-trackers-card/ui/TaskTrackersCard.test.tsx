import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { TaskTrackersCard } from "./TaskTrackersCard";

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const fetchTaskTrackerConnections = vi.fn<() => Promise<unknown>>();
const fetchTaskTrackerBoards = vi.fn<(tracker: string) => Promise<unknown>>();
const setTaskTrackerConnection =
  vi.fn<(tracker: string, request: unknown) => Promise<unknown>>();
const deleteTaskTrackerConnection =
  vi.fn<(tracker: string) => Promise<void>>();
const setTaskTrackerBoards =
  vi.fn<(tracker: string, request: unknown) => Promise<unknown>>();

// The queries hook imports its API from a relative path; mocking the resolved
// module intercepts it. The public barrel keeps re-exporting real hooks/meta.
vi.mock("@/entities/task-tracker/api/task-tracker-api", () => ({
  fetchTaskTrackerConnections: () => fetchTaskTrackerConnections(),
  fetchTaskTrackerBoards: (tracker: string) => fetchTaskTrackerBoards(tracker),
  setTaskTrackerConnection: (tracker: string, request: unknown) =>
    setTaskTrackerConnection(tracker, request),
  deleteTaskTrackerConnection: (tracker: string) =>
    deleteTaskTrackerConnection(tracker),
  setTaskTrackerBoards: (tracker: string, request: unknown) =>
    setTaskTrackerBoards(tracker, request)
}));

function connections(rows: Record<string, unknown>[]) {
  return { connections: rows };
}

describe("TaskTrackersCard", () => {
  beforeEach(() => {
    fetchTaskTrackerConnections.mockReset();
    fetchTaskTrackerBoards.mockReset();
    setTaskTrackerConnection.mockReset();
    deleteTaskTrackerConnection.mockReset();
    setTaskTrackerBoards.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит not_configured трекер с формой подключения", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        {
          tracker: "kaiten",
          display_name: "Kaiten",
          state: "not_configured"
        }
      ])
    );

    render(<TaskTrackersCard />);

    await waitFor(() => {
      expect(screen.getByText("Kaiten")).toBeTruthy();
    });
    expect(screen.getByText("Не настроено")).toBeTruthy();
    expect(screen.getByTestId("task-tracker-base-url-kaiten")).toBeTruthy();
    expect(screen.getByTestId("task-tracker-token-kaiten")).toBeTruthy();
    expect(
      screen.getByRole("button", { name: /Подключить трекер/ })
    ).toBeTruthy();
    // Boards-запрос не должен дёргаться для неподключённого трекера.
    expect(fetchTaskTrackerBoards).not.toHaveBeenCalled();
  });

  it("показывает inline-ошибку и не вызывает мутацию при пустом токене", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        { tracker: "kaiten", display_name: "Kaiten", state: "not_configured" }
      ])
    );

    render(<TaskTrackersCard />);

    const baseUrl = await screen.findByTestId<HTMLInputElement>(
      "task-tracker-base-url-kaiten"
    );
    fireEvent.change(baseUrl, {
      target: { value: "https://acme.kaiten.ru" }
    });
    fireEvent.click(screen.getByTestId("task-tracker-connect-kaiten"));

    expect(screen.getByTestId("task-tracker-error-kaiten").textContent).toMatch(
      /токен/i
    );
    expect(setTaskTrackerConnection).not.toHaveBeenCalled();
  });

  it("отправляет base_url + token при «Подключить»", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        { tracker: "kaiten", display_name: "Kaiten", state: "not_configured" }
      ])
    );
    setTaskTrackerConnection.mockResolvedValue({
      tracker: "kaiten",
      display_name: "Kaiten",
      state: "connected",
      base_url: "https://acme.kaiten.ru"
    });

    render(<TaskTrackersCard />);

    fireEvent.change(
      await screen.findByTestId("task-tracker-base-url-kaiten"),
      { target: { value: "https://acme.kaiten.ru" } }
    );
    fireEvent.change(screen.getByTestId("task-tracker-token-kaiten"), {
      target: { value: "secret-token" }
    });
    fireEvent.click(screen.getByTestId("task-tracker-connect-kaiten"));

    await waitFor(() => {
      expect(setTaskTrackerConnection).toHaveBeenCalledWith("kaiten", {
        base_url: "https://acme.kaiten.ru",
        token: "secret-token"
      });
    });
  });

  it("рендерит connected трекер с base_url и селектором досок", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        {
          tracker: "kaiten",
          display_name: "Kaiten",
          state: "connected",
          base_url: "https://acme.kaiten.ru"
        }
      ])
    );
    fetchTaskTrackerBoards.mockResolvedValue({
      tracker: "kaiten",
      spaces: [
        {
          space_id: "s1",
          space_title: "Engineering",
          boards: [
            {
              board_id: "b1",
              board_title: "Backlog",
              selected: true,
              context_field: "lane"
            }
          ]
        }
      ]
    });

    render(<TaskTrackersCard />);

    await waitFor(() => {
      expect(screen.getByText("Подключено")).toBeTruthy();
    });
    expect(screen.getByText(/acme\.kaiten\.ru/)).toBeTruthy();
    expect(
      screen.getByRole("button", { name: /Отключить трекер/ })
    ).toBeTruthy();

    // Boards подгружаются только для connected — селектор появляется.
    await waitFor(() => {
      expect(fetchTaskTrackerBoards).toHaveBeenCalledWith("kaiten");
    });
    expect(await screen.findByText("Engineering")).toBeTruthy();
    expect(screen.getByText("Backlog")).toBeTruthy();
    const checkbox = screen.getByTestId<HTMLInputElement>(
      "task-tracker-board-kaiten-s1:b1"
    );
    expect(checkbox.checked).toBe(true);
    expect(
      screen.getByTestId("task-tracker-boards-save-kaiten")
    ).toBeTruthy();
  });

  it("собирает выбранные доски и шлёт их при «Сохранить доски»", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        {
          tracker: "kaiten",
          display_name: "Kaiten",
          state: "connected",
          base_url: "https://acme.kaiten.ru"
        }
      ])
    );
    fetchTaskTrackerBoards.mockResolvedValue({
      tracker: "kaiten",
      spaces: [
        {
          space_id: "s1",
          space_title: "Engineering",
          boards: [
            {
              board_id: "b1",
              board_title: "Backlog",
              selected: true,
              context_field: "lane"
            },
            {
              board_id: "b2",
              board_title: "Done",
              selected: false,
              context_field: "none"
            }
          ]
        }
      ]
    });
    setTaskTrackerBoards.mockResolvedValue({ tracker: "kaiten", spaces: [] });

    render(<TaskTrackersCard />);

    await screen.findByText("Backlog");
    fireEvent.click(screen.getByTestId("task-tracker-boards-save-kaiten"));

    await waitFor(() => {
      expect(setTaskTrackerBoards).toHaveBeenCalledWith("kaiten", {
        boards: [
          {
            space_id: "s1",
            space_title: "Engineering",
            board_id: "b1",
            board_title: "Backlog",
            context_field: "lane"
          }
        ]
      });
    });
  });

  it("показывает ошибку probe для invalid состояния", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        {
          tracker: "kaiten",
          display_name: "Kaiten",
          state: "invalid",
          error: "Token rejected by Kaiten"
        }
      ])
    );

    render(<TaskTrackersCard />);

    await waitFor(() => {
      expect(screen.getByText("Токен отклонён")).toBeTruthy();
    });
    expect(
      screen.getByTestId("task-tracker-error-kaiten").textContent
    ).toMatch(/Token rejected by Kaiten/);
    expect(fetchTaskTrackerBoards).not.toHaveBeenCalled();
  });
});
