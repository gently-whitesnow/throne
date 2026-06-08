import { act, cleanup, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { RepositoryBinding } from "@/entities/repository-binding";

import { RepositoryBindingsList } from "./RepositoryBindingsList";

const listIntentRepositories =
  vi.fn<(intentId: string) => Promise<RepositoryBinding[]>>();

// Hook reads through the relative path inside the entity layer; mocking the
// API module keeps selectors / hook / meta exported by the public barrel.
vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listIntentRepositories: (intentId: string) =>
    listIntentRepositories(intentId),
  unbindIntentRepository: vi.fn().mockResolvedValue(undefined),
  bindIntentRepository: vi.fn(),
  searchGithubRepositories: vi.fn(),
  listMyGithubRepositories: vi.fn()
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
    | ((payload: unknown) => void)[]
    | undefined;
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

describe("RepositoryBindingsList", () => {
  beforeEach(() => {
    listIntentRepositories.mockReset();
    for (const k of Object.keys(realtimeHandlers)) {
      realtimeHandlers[k] = [];
    }
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает пустое состояние, когда binding'ов нет", async () => {
    listIntentRepositories.mockResolvedValue([]);
    renderWithQuery(<RepositoryBindingsList intentId="intent-1" />);
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
    renderWithQuery(<RepositoryBindingsList intentId="intent-1" />);
    await waitFor(() => {
      expect(screen.getByText("octocat/hello-world")).toBeTruthy();
    });
    const pill = screen.getByTestId("binding-clone-status-b1");
    expect(pill.getAttribute("data-status")).toBe("ready");
    expect(screen.getByTestId("binding-pr-b1").textContent).toMatch(/#42/);
    expect(screen.getByTestId("binding-pr-b1").textContent).toMatch(/Open/);
  });

  it("synthetic SSE-event intent.repository_clone_progress обновляет статус в DOM", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({ clone_status: "cloning" })
    ]);
    renderWithQuery(<RepositoryBindingsList intentId="intent-1" />);

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
    renderWithQuery(<RepositoryBindingsList intentId="intent-1" />);
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
    renderWithQuery(<RepositoryBindingsList intentId="intent-1" />);
    await waitFor(() => {
      expect(screen.getByTestId("binding-error-b1").textContent).toMatch(
        /permission denied/
      );
    });
  });

  it("intent.repository_bound добавляет новую строку", async () => {
    listIntentRepositories
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        makeBinding({ id: "b2", repo: "new-repo", clone_status: "pending" })
      ]);
    renderWithQuery(<RepositoryBindingsList intentId="intent-1" />);
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
