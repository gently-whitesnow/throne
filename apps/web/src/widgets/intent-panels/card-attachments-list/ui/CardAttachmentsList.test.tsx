import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import { HttpError } from "@/shared/api";
import type {
  AttachCardRequest,
  CardAttachment
} from "@/entities/task-tracker-card";

import { CardAttachmentsList } from "./CardAttachmentsList";

const listIntentCardAttachments =
  vi.fn<(intentId: string) => Promise<CardAttachment[]>>();
const detachIntentCard =
  vi.fn<(intentId: string, attachmentId: string) => Promise<void>>();
const refreshIntentCard =
  vi.fn<(intentId: string, attachmentId: string) => Promise<CardAttachment>>();
const attachIntentCard =
  vi.fn<
    (intentId: string, body: AttachCardRequest) => Promise<CardAttachment>
  >();

vi.mock("@/entities/task-tracker-card/api/card-attachments-api", () => ({
  listIntentCardAttachments: (intentId: string) =>
    listIntentCardAttachments(intentId),
  detachIntentCard: (intentId: string, attachmentId: string) =>
    detachIntentCard(intentId, attachmentId),
  refreshIntentCard: (intentId: string, attachmentId: string) =>
    refreshIntentCard(intentId, attachmentId),
  attachIntentCard: (intentId: string, body: AttachCardRequest) =>
    attachIntentCard(intentId, body)
}));

// Attach-форма читает подключённые трекеры и доски через entities/task-tracker;
// мокаем его api-модуль, чтобы форме было из чего выбрать координату.
vi.mock("@/entities/task-tracker/api/task-tracker-api", () => ({
  fetchTaskTrackerConnections: () =>
    Promise.resolve({
      connections: [
        {
          tracker: "kaiten",
          display_name: "Kaiten",
          state: "connected"
        }
      ]
    }),
  fetchTaskTrackerBoardSelection: () =>
    Promise.resolve({
      tracker: "kaiten",
      boards: [
        {
          space_id: "s1",
          space_title: "Space",
          board_id: "42",
          board_title: "Board 42",
          context_field: "none"
        }
      ]
    }),
  searchTaskTrackerBoards: vi.fn(),
  setTaskTrackerConnection: vi.fn(),
  deleteTaskTrackerConnection: vi.fn(),
  setTaskTrackerBoards: vi.fn()
}));

function makeCard(overrides: Partial<CardAttachment> = {}): CardAttachment {
  return {
    id: "c1",
    intent_id: "intent-1",
    tracker: "kaiten",
    board_id: "42",
    card_id: "777",
    title: "Починить логин",
    description: "## Шаги\n- воспроизвести",
    column_title: "In progress",
    archived: false,
    card_version: "v1",
    availability: "available",
    fetched_at: "2026-05-20T10:00:00Z",
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z",
    ...overrides
  };
}

function renderList() {
  // Карточки без realtime by design — RealtimeQueryBridge (EventSource) в jsdom
  // не нужен и падает, поэтому монтируем без моста.
  return renderWithQuery(
    <MemoryRouter>
      <CardAttachmentsList intentId="intent-1" />
    </MemoryRouter>,
    { withBridge: false }
  );
}

function openRowMenu() {
  fireEvent.click(screen.getByRole("button", { name: /Ещё действия/ }));
}

describe("CardAttachmentsList", () => {
  beforeEach(() => {
    listIntentCardAttachments.mockReset();
    detachIntentCard.mockReset();
    refreshIntentCard.mockReset();
    attachIntentCard.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("показывает пустое состояние, когда карточек нет", async () => {
    listIntentCardAttachments.mockResolvedValue([]);
    renderList();
    await waitFor(() => {
      expect(screen.getByText(/Карточки не приложены/)).toBeTruthy();
    });
  });

  it("рендерит title, координату и availability-бейдж", async () => {
    listIntentCardAttachments.mockResolvedValue([
      makeCard({ availability: "unavailable", archived: true })
    ]);
    renderList();
    await waitFor(() => {
      expect(screen.getByText("Починить логин")).toBeTruthy();
    });
    const badge = screen.getByTestId("card-availability-c1");
    expect(badge.getAttribute("data-availability")).toBe("unavailable");
    expect(screen.getByTestId("card-archived-c1")).toBeTruthy();
    expect(screen.getByText(/kaiten · 42 · 777/)).toBeTruthy();
  });

  it("«Отвязать» вызывает detach и обновляет список", async () => {
    listIntentCardAttachments
      .mockResolvedValueOnce([makeCard()])
      .mockResolvedValueOnce([]);
    detachIntentCard.mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValue(true);
    renderList();

    await waitFor(() => {
      expect(screen.getByText("Починить логин")).toBeTruthy();
    });

    openRowMenu();
    fireEvent.click(screen.getByRole("menuitem", { name: /Отвязать/ }));

    await waitFor(() => {
      expect(detachIntentCard).toHaveBeenCalledWith("intent-1", "c1");
    });
    await waitFor(() => {
      expect(screen.getByText(/Карточки не приложены/)).toBeTruthy();
    });
  });

  it("«Обновить» вызывает refresh и переопрашивает список", async () => {
    listIntentCardAttachments
      .mockResolvedValueOnce([makeCard({ availability: "available" })])
      .mockResolvedValueOnce([makeCard({ availability: "gone" })]);
    refreshIntentCard.mockResolvedValue(makeCard({ availability: "gone" }));
    renderList();

    await waitFor(() => {
      expect(
        screen
          .getByTestId("card-availability-c1")
          .getAttribute("data-availability")
      ).toBe("available");
    });

    openRowMenu();
    fireEvent.click(screen.getByRole("menuitem", { name: /Обновить/ }));

    await waitFor(() => {
      expect(refreshIntentCard).toHaveBeenCalledWith("intent-1", "c1");
    });
    await waitFor(() => {
      expect(
        screen
          .getByTestId("card-availability-c1")
          .getAttribute("data-availability")
      ).toBe("gone");
    });
  });

  it("attach: доска из board-selection + card_id → POST с координатой", async () => {
    listIntentCardAttachments
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([makeCard()]);
    attachIntentCard.mockResolvedValue(makeCard());
    renderList();

    await waitFor(() => {
      expect(screen.getByText(/Карточки не приложены/)).toBeTruthy();
    });

    fireEvent.click(screen.getByTestId("attach-card-open"));
    await screen.findByText(/Board 42/);
    fireEvent.change(screen.getByLabelText("ID карточки"), {
      target: { value: " 777 " }
    });
    fireEvent.click(screen.getByRole("button", { name: /^Приложить$/ }));

    await waitFor(() => {
      expect(attachIntentCard).toHaveBeenCalledWith("intent-1", {
        tracker: "kaiten",
        board_id: "42",
        card_id: "777"
      });
    });
    await waitFor(() => {
      expect(screen.getByText("Починить логин")).toBeTruthy();
    });
  });

  it("attach: 502 показывает типизированную ошибку недоступности трекера", async () => {
    listIntentCardAttachments.mockResolvedValue([]);
    attachIntentCard.mockRejectedValue(new HttpError(502, "/x", "bad gateway"));
    renderList();

    await waitFor(() => {
      expect(screen.getByText(/Карточки не приложены/)).toBeTruthy();
    });

    fireEvent.click(screen.getByTestId("attach-card-open"));
    await screen.findByText(/Board 42/);
    fireEvent.change(screen.getByLabelText("ID карточки"), {
      target: { value: "777" }
    });
    fireEvent.click(screen.getByRole("button", { name: /^Приложить$/ }));

    await waitFor(() => {
      expect(screen.getByRole("alert").textContent).toMatch(
        /Трекер недоступен/
      );
    });
  });
});
