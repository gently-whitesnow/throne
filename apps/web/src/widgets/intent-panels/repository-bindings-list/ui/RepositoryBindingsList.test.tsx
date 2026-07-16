import {
  act,
  cleanup,
  fireEvent,
  screen,
  waitFor
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { RepositoryBinding } from "@/entities/repository-binding";

import { RepositoryBindingsList } from "./RepositoryBindingsList";

const listIntentRepositories =
  vi.fn<(intentId: string) => Promise<RepositoryBinding[]>>();
const refreshIntentRepository =
  vi.fn<(intentId: string, bindingId: string) => Promise<RepositoryBinding>>();
const syncIntentRepositoryBranch =
  vi.fn<(intentId: string, bindingId: string) => Promise<RepositoryBinding>>();

// Hook reads through the relative path inside the entity layer; mocking the
// API module keeps selectors / hook / meta exported by the public barrel.
vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listIntentRepositories: (intentId: string) =>
    listIntentRepositories(intentId),
  refreshIntentRepository: (intentId: string, bindingId: string) =>
    refreshIntentRepository(intentId, bindingId),
  syncIntentRepositoryBranch: (intentId: string, bindingId: string) =>
    syncIntentRepositoryBranch(intentId, bindingId),
  unbindIntentRepository: vi.fn().mockResolvedValue(undefined),
  bindIntentRepository: vi.fn()
}));

// Capture realtime subscriptions so individual tests can fire synthetic SSE
// payloads without spinning up an EventSource in jsdom.
type RealtimeHandlers = Record<string, ((payload: unknown) => void)[]>;
const realtimeHandlers: RealtimeHandlers = {};

vi.mock("@/shared/realtime", () => ({
  useRealtimeEvent: (name: string, handler: (payload: unknown) => void) => {
    const list = realtimeHandlers[name] ?? [];
    list.push(handler);
    realtimeHandlers[name] = list;
  }
}));

function emit(name: string, payload: unknown) {
  const list = realtimeHandlers[name] as
    ((payload: unknown) => void)[] | undefined;
  if (list === undefined) return;
  for (const fn of list) fn(payload);
}

function makeBinding(
  overrides: Partial<RepositoryBinding> = {}
): RepositoryBinding {
  return {
    id: "b1",
    intent_id: "intent-1",
    provider: "github",
    host: "github.com",
    owner: "octocat",
    repo: "hello-world",
    default_branch: "main",
    workspace_path: "/tmp/throne/intent-1/b1",
    clone_status: "cloning",
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z",
    ...overrides
  };
}

function renderList() {
  return renderWithQuery(
    <MemoryRouter>
      <RepositoryBindingsList intentId="intent-1" />
    </MemoryRouter>
  );
}

describe("RepositoryBindingsList", () => {
  beforeEach(() => {
    listIntentRepositories.mockReset();
    refreshIntentRepository.mockReset();
    syncIntentRepositoryBranch.mockReset();
    for (const k of Object.keys(realtimeHandlers)) {
      realtimeHandlers[k] = [];
    }
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает пустое состояние, когда binding'ов нет", async () => {
    listIntentRepositories.mockResolvedValue([]);
    renderList();
    await waitFor(() => {
      expect(screen.getByText(/Репозитории не привязаны/)).toBeTruthy();
    });
  });

  it("рендерит binding с clone_status pill и PR chip", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({
        clone_status: "ready",
        pull_request_number: 42,
        pull_request_state: "open"
      })
    ]);
    renderList();
    await waitFor(() => {
      expect(screen.getByText("octocat/hello-world")).toBeTruthy();
    });
    const pill = screen.getByTestId("binding-clone-status-b1");
    expect(pill.getAttribute("data-status")).toBe("ready");
    expect(screen.getByTestId("binding-pr-b1").textContent).toMatch(/#42/);
    expect(screen.getByTestId("binding-pr-b1").textContent).toMatch(/Open/);
  });

  it("показывает внутреннее ревью и внешнюю ссылку на PR в строке binding'а", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({
        clone_status: "ready",
        pull_request_number: 42,
        pull_request_state: "open"
      })
    ]);
    renderList();

    const reviewLink = await screen.findByRole("link", {
      name: /Открыть ревью octocat\/hello-world/
    });
    expect(reviewLink.getAttribute("href")).toBe("/intents/intent-1/review/b1");

    const externalLink = screen.getByRole("link", {
      name: /Внешняя ссылка на PR octocat\/hello-world/
    });
    expect(externalLink.getAttribute("href")).toBe(
      "https://github.com/octocat/hello-world/pull/42"
    );
    expect(screen.queryByText("Просмотреть PR")).toBeNull();
  });

  it("synthetic SSE-event intent.repository_clone_progress обновляет статус в DOM", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({ clone_status: "cloning" })
    ]);
    renderList();

    await waitFor(() => {
      const pill = screen.getByTestId("binding-clone-status-b1");
      expect(pill.getAttribute("data-status")).toBe("cloning");
    });

    act(() => {
      emit("intent.repository_clone_progress", {
        intent_id: "intent-1",
        binding_id: "b1",
        status: "ready"
      });
    });

    await waitFor(() => {
      const pill = screen.getByTestId("binding-clone-status-b1");
      expect(pill.getAttribute("data-status")).toBe("ready");
    });
  });

  it("intent.repository_unbound для другого интента не трогает список", async () => {
    listIntentRepositories.mockResolvedValue([makeBinding()]);
    renderList();
    await waitFor(() => {
      expect(screen.getByText("octocat/hello-world")).toBeTruthy();
    });

    act(() => {
      emit("intent.repository_unbound", {
        intent_id: "other-intent",
        binding_id: "b1"
      });
    });

    expect(screen.getByText("octocat/hello-world")).toBeTruthy();
  });

  it("показывает clone_error для failed binding'а", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({
        clone_status: "failed",
        clone_error: "permission denied"
      })
    ]);
    renderList();
    await waitFor(() => {
      expect(screen.getByTestId("binding-error-b1").textContent).toMatch(
        /permission denied/
      );
    });
  });

  it("«Обновить» дёргает refresh-эндпоинт и патчит статус строки из ответа", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({ clone_status: "ready" })
    ]);
    refreshIntentRepository.mockResolvedValue(
      makeBinding({ clone_status: "pending" })
    );
    renderList();

    await waitFor(() => {
      const pill = screen.getByTestId("binding-clone-status-b1");
      expect(pill.getAttribute("data-status")).toBe("ready");
    });

    // «Обновить» теперь живёт в overflow-меню строки — сперва раскрываем его.
    fireEvent.click(
      screen.getByLabelText(/Ещё действия — octocat\/hello-world/)
    );
    fireEvent.click(screen.getByLabelText(/Обновить octocat\/hello-world/));

    await waitFor(() => {
      expect(refreshIntentRepository).toHaveBeenCalledWith("intent-1", "b1");
      const pill = screen.getByTestId("binding-clone-status-b1");
      expect(pill.getAttribute("data-status")).toBe("pending");
    });
  });

  it("«Синхронизировать ветку» подтверждается и дёргает sync-branch-эндпоинт", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    listIntentRepositories.mockResolvedValue([
      makeBinding({ clone_status: "ready" })
    ]);
    syncIntentRepositoryBranch.mockResolvedValue(
      makeBinding({ clone_status: "ready" })
    );
    renderList();

    await waitFor(() => {
      expect(screen.getByTestId("binding-clone-status-b1")).toBeTruthy();
    });

    fireEvent.click(
      screen.getByLabelText(/Ещё действия — octocat\/hello-world/)
    );
    fireEvent.click(
      screen.getByLabelText(/Синхронизировать ветку octocat\/hello-world/)
    );

    await waitFor(() => {
      expect(confirmSpy).toHaveBeenCalled();
      expect(syncIntentRepositoryBranch).toHaveBeenCalledWith("intent-1", "b1");
    });
    confirmSpy.mockRestore();
  });

  it("«Синхронизировать ветку»: отмена в confirm не дёргает эндпоинт", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(false);
    listIntentRepositories.mockResolvedValue([
      makeBinding({ clone_status: "ready" })
    ]);
    renderList();

    await waitFor(() => {
      expect(screen.getByTestId("binding-clone-status-b1")).toBeTruthy();
    });

    fireEvent.click(
      screen.getByLabelText(/Ещё действия — octocat\/hello-world/)
    );
    fireEvent.click(
      screen.getByLabelText(/Синхронизировать ветку octocat\/hello-world/)
    );

    expect(confirmSpy).toHaveBeenCalled();
    expect(syncIntentRepositoryBranch).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it("intent.repository_bound добавляет новую строку", async () => {
    listIntentRepositories
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        makeBinding({ id: "b2", repo: "new-repo", clone_status: "pending" })
      ]);
    renderList();
    await waitFor(() => {
      expect(screen.getByText(/Репозитории не привязаны/)).toBeTruthy();
    });

    act(() => {
      emit(
        "intent.repository_bound",
        makeBinding({ id: "b2", repo: "new-repo", clone_status: "pending" })
      );
    });

    await waitFor(() => {
      expect(screen.getByText("octocat/new-repo")).toBeTruthy();
    });
  });
});
