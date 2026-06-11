import {
  act,
  cleanup,
  fireEvent,
  screen,
  waitFor
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type {
  PullRequestComment,
  PullRequestSyncResult
} from "@/entities/pull-request-comment";
import { renderWithQuery } from "@/app/test-utils";
import type { RepositoryBinding } from "@/entities/repository-binding";

import { PullRequestCommentsSection } from "./PullRequestCommentsSection";

const listIntentRepositories =
  vi.fn<(intentId: string) => Promise<RepositoryBinding[]>>();
const listPullRequestComments =
  vi.fn<
    (intentId: string, bindingId: string) => Promise<PullRequestComment[]>
  >();
const syncPullRequest =
  vi.fn<
    (intentId: string, bindingId: string) => Promise<PullRequestSyncResult>
  >();

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listIntentRepositories: (intentId: string) =>
    listIntentRepositories(intentId),
  unbindIntentRepository: vi.fn(),
  bindIntentRepository: vi.fn()
}));

vi.mock("@/entities/pull-request-comment/api/pr-comments-api", () => ({
  listPullRequestComments: (intentId: string, bindingId: string) =>
    listPullRequestComments(intentId, bindingId),
  syncPullRequest: (intentId: string, bindingId: string) =>
    syncPullRequest(intentId, bindingId)
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

// Секция свёрнута по умолчанию — раскрываем её перед проверкой тела.
async function expandPrComments() {
  fireEvent.click(await screen.findByRole("button", { name: /PR comments/i }));
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
    clone_status: "ready",
    pull_request_number: 42,
    pull_request_state: "open",
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z",
    ...overrides
  };
}

function makeComment(
  overrides: Partial<PullRequestComment> = {}
): PullRequestComment {
  return {
    id: "c1",
    binding_id: "b1",
    author_login: "alice",
    body: "looks good",
    created_at: "2026-05-21T10:00:00Z",
    ...overrides
  };
}

describe("PullRequestCommentsSection", () => {
  beforeEach(() => {
    listIntentRepositories.mockReset();
    listPullRequestComments.mockReset();
    syncPullRequest.mockReset();
    for (const k of Object.keys(realtimeHandlers)) {
      realtimeHandlers[k] = [];
    }
  });

  afterEach(() => {
    cleanup();
  });

  it("скрывает секцию, если у интента нет ни одного PR-binding'а", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({
        pull_request_number: undefined,
        pull_request_state: undefined
      })
    ]);
    const { container } = renderWithQuery(
      <PullRequestCommentsSection intentId="intent-1" />
    );
    await waitFor(() => {
      expect(listIntentRepositories).toHaveBeenCalledWith("intent-1");
    });
    expect(
      container.querySelector('[data-testid="pr-comments-section"]')
    ).toBeNull();
  });

  it("рендерит карточку с автором, телом и created_at", async () => {
    listIntentRepositories
      .mockResolvedValueOnce([makeBinding()])
      .mockResolvedValueOnce([
        makeBinding({
          pull_request_state: "closed",
          updated_at: "2026-05-22T10:00:00Z"
        })
      ]);
    listPullRequestComments.mockResolvedValue([
      makeComment({
        author_login: "alice",
        body: "needs a test",
        created_at: "2026-05-21T10:00:00Z"
      })
    ]);
    renderWithQuery(<PullRequestCommentsSection intentId="intent-1" />);
    await expandPrComments();

    await waitFor(() => {
      expect(screen.getByTestId("pr-comments-card-b1")).toBeTruthy();
    });
    await waitFor(() => {
      expect(screen.getByTestId("pr-comment-c1")).toBeTruthy();
    });
    expect(screen.getByTestId("pr-comment-c1").textContent).toMatch(/alice/);
    expect(screen.getByTestId("pr-comment-body-c1").textContent).toMatch(
      /needs a test/
    );
    expect(screen.getByTestId("pr-comments-pr-b1").textContent).toMatch(/#42/);
  });

  it("synthetic SSE-event intent.pr_comment_added добавляет коммент в DOM без рефреша", async () => {
    listIntentRepositories
      .mockResolvedValueOnce([makeBinding()])
      .mockResolvedValueOnce([
        makeBinding({
          pull_request_state: "closed",
          updated_at: "2026-05-22T10:00:00Z"
        })
      ]);
    listPullRequestComments.mockResolvedValue([
      makeComment({ id: "c1", body: "first" })
    ]);
    renderWithQuery(<PullRequestCommentsSection intentId="intent-1" />);
    await expandPrComments();

    await waitFor(() => {
      expect(screen.getByTestId("pr-comment-c1")).toBeTruthy();
    });

    act(() => {
      emit("intent.pr_comment_added", {
        intent_id: "intent-1",
        binding_id: "b1",
        comment: makeComment({
          id: "c2",
          body: "second",
          created_at: "2026-05-22T10:00:00Z"
        })
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId("pr-comment-c2")).toBeTruthy();
    });
    // Хук не должен дёргать повторный GET — реалтайм-payload встраивается в state.
    expect(listPullRequestComments).toHaveBeenCalledTimes(1);
  });

  it("intent.pr_comment_added для чужого binding'а игнорируется", async () => {
    listIntentRepositories.mockResolvedValue([makeBinding()]);
    listPullRequestComments.mockResolvedValue([makeComment()]);
    renderWithQuery(<PullRequestCommentsSection intentId="intent-1" />);
    await expandPrComments();

    await waitFor(() => {
      expect(screen.getByTestId("pr-comment-c1")).toBeTruthy();
    });

    act(() => {
      emit("intent.pr_comment_added", {
        intent_id: "intent-1",
        binding_id: "other-binding",
        comment: makeComment({ id: "c999", body: "leak" })
      });
    });

    expect(screen.queryByTestId("pr-comment-c999")).toBeNull();
  });

  it("кнопка «Обновить» дёргает syncPullRequest и перечитывает список", async () => {
    listIntentRepositories
      .mockResolvedValueOnce([makeBinding()])
      .mockResolvedValueOnce([
        makeBinding({
          pull_request_state: "closed",
          updated_at: "2026-05-22T10:00:00Z"
        })
      ]);
    listPullRequestComments
      .mockResolvedValueOnce([makeComment({ id: "c1", body: "first" })])
      .mockResolvedValueOnce([
        makeComment({ id: "c1", body: "first" }),
        makeComment({
          id: "c2",
          body: "fresh",
          created_at: "2026-05-22T10:00:00Z"
        })
      ]);
    syncPullRequest.mockResolvedValue({
      binding_id: "b1",
      new_comments: 1,
      total_comments: 2,
      pull_request_state: "closed",
      comments: []
    });

    renderWithQuery(<PullRequestCommentsSection intentId="intent-1" />);
    await expandPrComments();
    await waitFor(() => {
      expect(screen.getByTestId("pr-comment-c1")).toBeTruthy();
    });

    const button = screen.getByRole("button", {
      name: /Обновить комментарии PR/
    });
    act(() => {
      button.click();
    });

    await waitFor(() => {
      expect(syncPullRequest).toHaveBeenCalledWith("intent-1", "b1");
    });
    await waitFor(() => {
      expect(screen.getByTestId("pr-comment-c2")).toBeTruthy();
    });
    await waitFor(() => {
      expect(screen.getByTestId("pr-comments-pr-b1").textContent).toMatch(
        /Closed/
      );
    });
  });

  it("показывает empty state, если у PR пока нет комментариев", async () => {
    listIntentRepositories.mockResolvedValue([makeBinding()]);
    listPullRequestComments.mockResolvedValue([]);
    renderWithQuery(<PullRequestCommentsSection intentId="intent-1" />);
    await expandPrComments();

    await waitFor(() => {
      expect(screen.getByTestId("pr-comments-empty-b1")).toBeTruthy();
    });
  });
});
