import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { TaskTrackersCard } from "./TaskTrackersCard";

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const fetchTaskTrackerConnections = vi.fn<() => Promise<unknown>>();
const searchTaskTrackerBoards =
  vi.fn<(tracker: string, params: unknown) => Promise<unknown>>();
const fetchTaskTrackerBoardSelection =
  vi.fn<(tracker: string) => Promise<unknown>>();
const setTaskTrackerConnection =
  vi.fn<(tracker: string, request: unknown) => Promise<unknown>>();
const deleteTaskTrackerConnection = vi.fn<(tracker: string) => Promise<void>>();
const setTaskTrackerBoards =
  vi.fn<(tracker: string, request: unknown) => Promise<unknown>>();

// The queries hook imports its API from a relative path; mocking the resolved
// module intercepts it. The public barrel keeps re-exporting real hooks/meta.
vi.mock("@/entities/task-tracker/api/task-tracker-api", () => ({
  fetchTaskTrackerConnections: () => fetchTaskTrackerConnections(),
  searchTaskTrackerBoards: (tracker: string, params: unknown) =>
    searchTaskTrackerBoards(tracker, params),
  fetchTaskTrackerBoardSelection: (tracker: string) =>
    fetchTaskTrackerBoardSelection(tracker),
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

const connectedRow = {
  tracker: "kaiten",
  display_name: "Kaiten",
  state: "connected",
  base_url: "https://acme.kaiten.ru"
};

describe("TaskTrackersCard", () => {
  beforeEach(() => {
    fetchTaskTrackerConnections.mockReset();
    searchTaskTrackerBoards.mockReset();
    fetchTaskTrackerBoardSelection.mockReset();
    setTaskTrackerConnection.mockReset();
    deleteTaskTrackerConnection.mockReset();
    setTaskTrackerBoards.mockReset();
    searchTaskTrackerBoards.mockResolvedValue({
      tracker: "kaiten",
      boards: []
    });
    fetchTaskTrackerBoardSelection.mockResolvedValue({
      tracker: "kaiten",
      boards: []
    });
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит not_configured трекер с формой подключения и не грузит доски", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        { tracker: "kaiten", display_name: "Kaiten", state: "not_configured" }
      ])
    );

    render(<TaskTrackersCard />);

    await waitFor(() => {
      expect(screen.getByText("Kaiten")).toBeTruthy();
    });
    expect(screen.getByTestId("task-tracker-base-url-kaiten")).toBeTruthy();
    expect(fetchTaskTrackerBoardSelection).not.toHaveBeenCalled();
    expect(searchTaskTrackerBoards).not.toHaveBeenCalled();
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

  it("рендерит сохранённую селекцию чипами с полем «контекст»", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(connections([connectedRow]));
    fetchTaskTrackerBoardSelection.mockResolvedValue({
      tracker: "kaiten",
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

    render(<TaskTrackersCard />);

    await waitFor(() => {
      expect(fetchTaskTrackerBoardSelection).toHaveBeenCalledWith("kaiten");
    });
    expect(await screen.findByText("Backlog")).toBeTruthy();
    expect(screen.getByText("Engineering")).toBeTruthy();
    const context = screen.getByTestId<HTMLSelectElement>(
      "task-tracker-context-kaiten-b1"
    );
    expect(context.value).toBe("lane");
  });

  it("добавляет доску через поиск и шлёт полный набор при «Сохранить»", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(connections([connectedRow]));
    searchTaskTrackerBoards.mockResolvedValue({
      tracker: "kaiten",
      boards: [
        {
          board_id: "b1",
          board_title: "Backlog",
          space_id: "s1",
          space_title: "Engineering"
        }
      ]
    });
    setTaskTrackerBoards.mockResolvedValue({ tracker: "kaiten", boards: [] });

    render(<TaskTrackersCard />);

    const input = await screen.findByTestId("task-tracker-board-search-kaiten");
    fireEvent.focus(input);

    const option = await screen.findByTestId(
      "task-tracker-board-option-kaiten-b1"
    );
    fireEvent.mouseDown(option);

    expect(
      await screen.findByTestId("task-tracker-board-chip-kaiten-b1")
    ).toBeTruthy();

    fireEvent.click(screen.getByTestId("task-tracker-boards-save-kaiten"));

    await waitFor(() => {
      expect(setTaskTrackerBoards).toHaveBeenCalledWith("kaiten", {
        boards: [
          {
            space_id: "s1",
            space_title: "Engineering",
            board_id: "b1",
            board_title: "Backlog",
            context_field: "none"
          }
        ]
      });
    });
  });

  it("удаляет чип и шлёт уменьшенный набор при «Сохранить»", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(connections([connectedRow]));
    fetchTaskTrackerBoardSelection.mockResolvedValue({
      tracker: "kaiten",
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
    setTaskTrackerBoards.mockResolvedValue({ tracker: "kaiten", boards: [] });

    render(<TaskTrackersCard />);

    await screen.findByText("Backlog");
    fireEvent.click(screen.getByTestId("task-tracker-board-remove-kaiten-b1"));
    fireEvent.click(screen.getByTestId("task-tracker-boards-save-kaiten"));

    await waitFor(() => {
      expect(setTaskTrackerBoards).toHaveBeenCalledWith("kaiten", {
        boards: []
      });
    });
  });

  it("показывает ошибку probe для auth состояния и не грузит доски", async () => {
    fetchTaskTrackerConnections.mockResolvedValue(
      connections([
        {
          tracker: "kaiten",
          display_name: "Kaiten",
          state: "auth",
          error: "Token rejected by Kaiten"
        }
      ])
    );

    render(<TaskTrackersCard />);

    await waitFor(() => {
      expect(screen.getByText("Переподключите")).toBeTruthy();
    });
    expect(screen.getByTestId("task-tracker-error-kaiten").textContent).toMatch(
      /Token rejected by Kaiten/
    );
    expect(fetchTaskTrackerBoardSelection).not.toHaveBeenCalled();
  });
});
