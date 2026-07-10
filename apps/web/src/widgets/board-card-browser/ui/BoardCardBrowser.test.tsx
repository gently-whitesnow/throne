import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import { HttpError } from "@/shared/api";
import type { TaskTrackerCard } from "@/entities/task-tracker-card";

import { BoardCardBrowser } from "./BoardCardBrowser";

const fetchBoardCards =
  vi.fn<(tracker: string, boardId: string) => Promise<TaskTrackerCard[]>>();
const fetchBoardCard =
  vi.fn<
    (
      tracker: string,
      boardId: string,
      cardId: string
    ) => Promise<TaskTrackerCard>
  >();

vi.mock("@/entities/task-tracker-card/api/board-cards-api", () => ({
  fetchBoardCards: (tracker: string, boardId: string) =>
    fetchBoardCards(tracker, boardId),
  fetchBoardCard: (tracker: string, boardId: string, cardId: string) =>
    fetchBoardCard(tracker, boardId, cardId)
}));

const httpPost = vi.fn<(...args: unknown[]) => Promise<unknown>>();
vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    httpPost: (...args: unknown[]) => httpPost(...args)
  };
});

const attachIntentCard =
  vi.fn<(intentId: string, body: unknown) => Promise<unknown>>();
vi.mock("@/entities/task-tracker-card/api/card-attachments-api", () => ({
  attachIntentCard: (intentId: string, body: unknown) =>
    attachIntentCard(intentId, body),
  listIntentCardAttachments: vi.fn(),
  detachIntentCard: vi.fn(),
  refreshIntentCard: vi.fn()
}));

const navigate = vi.fn();
vi.mock("react-router-dom", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router-dom")>();
  return { ...actual, useNavigate: () => navigate };
});

function makeCard(overrides: Partial<TaskTrackerCard> = {}): TaskTrackerCard {
  return {
    card_id: "777",
    board_id: "42",
    title: "Починить логин",
    column_title: "In progress",
    archived: false,
    ...overrides
  };
}

function renderBrowser() {
  return renderWithQuery(
    <MemoryRouter initialEntries={["/?context=__board:kaiten:42"]}>
      <BoardCardBrowser tracker="kaiten" boardId="42" />
    </MemoryRouter>,
    { withBridge: false }
  );
}

describe("BoardCardBrowser", () => {
  beforeEach(() => {
    fetchBoardCards.mockReset();
    fetchBoardCard.mockReset();
    httpPost.mockReset();
    attachIntentCard.mockReset();
    navigate.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит список карточек и открывает детальную карточку", async () => {
    fetchBoardCards.mockResolvedValue([makeCard()]);
    fetchBoardCard.mockResolvedValue(
      makeCard({ description: "## Шаги\n- воспроизвести" })
    );

    renderBrowser();

    const row = await screen.findByRole("button", { name: /Починить логин/ });
    fireEvent.click(row);

    expect(
      await screen.findByRole("button", {
        name: "Создать интент с этой карточкой"
      })
    ).toBeTruthy();
    expect(await screen.findByText(/Шаги/)).toBeTruthy();
  });

  it("показывает дружелюбное сообщение при деградации (409), а не пустой экран", async () => {
    fetchBoardCards.mockRejectedValue(
      new HttpError(409, "/cards", "not connected")
    );

    renderBrowser();

    expect(
      await screen.findByText(/Трекер не подключён или токен отклонён/)
    ).toBeTruthy();
  });

  it("рендерит title карточки как внешнюю ссылку, когда web_url задан", async () => {
    fetchBoardCards.mockResolvedValue([makeCard()]);
    fetchBoardCard.mockResolvedValue(
      makeCard({ web_url: "https://acme.kaiten.ru/777", description: "desc" })
    );

    renderBrowser();

    fireEvent.click(
      await screen.findByRole("button", { name: /Починить логин/ })
    );

    const link = await screen.findByTestId("board-card-link-777");
    expect(link.tagName).toBe("A");
    expect(link.getAttribute("href")).toBe("https://acme.kaiten.ru/777");
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.getAttribute("rel")).toBe("noopener noreferrer");
    expect(link.textContent).toBe("Починить логин");
  });

  it("рендерит title карточки без ссылки, когда web_url отсутствует", async () => {
    fetchBoardCards.mockResolvedValue([makeCard()]);
    fetchBoardCard.mockResolvedValue(makeCard({ description: "desc" }));

    renderBrowser();

    fireEvent.click(
      await screen.findByRole("button", { name: /Починить логин/ })
    );

    await screen.findByRole("button", {
      name: "Создать интент с этой карточкой"
    });
    expect(screen.queryByTestId("board-card-link-777")).toBeNull();
  });

  it("создаёт интент, привязывает карточку и переходит к нему", async () => {
    fetchBoardCards.mockResolvedValue([makeCard()]);
    fetchBoardCard.mockResolvedValue(makeCard({ description: "desc" }));
    httpPost.mockResolvedValue({ id: "new-intent" });
    attachIntentCard.mockResolvedValue({});

    renderBrowser();

    fireEvent.click(
      await screen.findByRole("button", { name: /Починить логин/ })
    );
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Создать интент с этой карточкой"
      })
    );

    await waitFor(() => {
      expect(httpPost).toHaveBeenCalledWith(expect.any(String), {
        title: "Починить логин",
        text: "Починить логин"
      });
    });
    await waitFor(() => {
      expect(attachIntentCard).toHaveBeenCalledWith("new-intent", {
        tracker: "kaiten",
        board_id: "42",
        card_id: "777"
      });
    });
    await waitFor(() => {
      expect(navigate).toHaveBeenCalledWith(
        expect.stringContaining("/intents/new-intent")
      );
    });
  });
});
