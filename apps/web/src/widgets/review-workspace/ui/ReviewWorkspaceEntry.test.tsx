import {
  cleanup,
  fireEvent,
  screen,
  waitFor,
  within
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { RepositoryBinding } from "@/entities/repository-binding";
import type {
  PullRequestCommit,
  PullRequestDiff,
  SubmitReviewCommentRequest
} from "@/entities/review-workspace";

import { ReviewWorkspaceEntry } from "./ReviewWorkspaceEntry";

const listIntentRepositories =
  vi.fn<(intentId: string) => Promise<RepositoryBinding[]>>();
const getReviewDiff = vi.fn<() => Promise<PullRequestDiff>>();
const listReviewCommits = vi.fn<() => Promise<PullRequestCommit[]>>();
const submitReviewComment =
  vi.fn<
    (
      intentId: string,
      bindingId: string,
      body: SubmitReviewCommentRequest
    ) => Promise<unknown>
  >();

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listIntentRepositories: (intentId: string) =>
    listIntentRepositories(intentId),
  unbindIntentRepository: vi.fn(),
  bindIntentRepository: vi.fn()
}));

vi.mock("@/entities/review-workspace/api/review-api", () => ({
  getReviewDiff: () => getReviewDiff(),
  listReviewCommits: () => listReviewCommits(),
  submitReviewComment: (
    intentId: string,
    bindingId: string,
    body: SubmitReviewCommentRequest
  ) => submitReviewComment(intentId, bindingId, body)
}));

vi.mock("@/entities/pull-request-comment/api/pr-comments-api", () => ({
  listPullRequestComments: () => Promise.resolve([]),
  syncPullRequest: () => Promise.resolve({ binding_id: "b1" })
}));

vi.mock("@/shared/realtime", () => ({
  useRealtimeEvent: () => undefined
}));

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

const DIFF: PullRequestDiff = {
  base_sha: "base111",
  head_sha: "head222",
  start_sha: "start333",
  files: [
    {
      path: "src/app.ts",
      status: "modified",
      patch: ["@@ -1,2 +1,2 @@", " keep", "-old", "+new"].join("\n")
    }
  ]
};

describe("ReviewWorkspaceEntry", () => {
  beforeEach(() => {
    listIntentRepositories.mockReset().mockResolvedValue([makeBinding()]);
    getReviewDiff.mockReset().mockResolvedValue(DIFF);
    listReviewCommits.mockReset().mockResolvedValue([]);
    submitReviewComment.mockReset().mockResolvedValue({
      id: "rc1",
      binding_id: "b1",
      author_login: "me",
      body: "nit",
      created_at: "2026-05-22T10:00:00Z"
    });
  });

  afterEach(() => {
    cleanup();
  });

  it("скрывает секцию без PR/MR-binding'ов", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({ pull_request_number: undefined })
    ]);
    renderWithQuery(<ReviewWorkspaceEntry intentId="intent-1" />);
    await waitFor(() => {
      expect(listIntentRepositories).toHaveBeenCalled();
    });
    expect(screen.queryByTestId("review-workspace-entry")).toBeNull();
  });

  it("показывает provider-neutral PR-лейбл для GitHub binding'а", async () => {
    renderWithQuery(<ReviewWorkspaceEntry intentId="intent-1" />);
    expect(await screen.findByText("PR #42")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Открыть ревью" })).toBeTruthy();
  });

  it("показывает MR-лейбл для GitLab binding'а", async () => {
    listIntentRepositories.mockResolvedValue([
      makeBinding({ provider: "gitlab" })
    ]);
    renderWithQuery(<ReviewWorkspaceEntry intentId="intent-1" />);
    expect(await screen.findByText("MR !42")).toBeTruthy();
  });

  it("открывает fullscreen overlay, грузит diff и отправляет inline-комментарий в провайдер", async () => {
    renderWithQuery(<ReviewWorkspaceEntry intentId="intent-1" />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Открыть ревью" })
    );

    // Fullscreen surface смонтирован, файл из diff виден.
    const dialog = await screen.findByRole("dialog", {
      name: "Review workspace"
    });
    expect(dialog).toBeTruthy();
    await waitFor(() => {
      expect(getReviewDiff).toHaveBeenCalled();
    });
    expect(await screen.findByText("new")).toBeTruthy();

    // Открыть composer именно на добавленной строке (right / newLine=2).
    const addedRow = screen.getByText("new").closest("div.group");
    expect(addedRow).not.toBeNull();
    fireEvent.click(
      within(addedRow as HTMLElement).getByRole("button", {
        name: "Добавить inline-комментарий"
      })
    );
    const textarea = await screen.findByPlaceholderText(
      "Inline-комментарий — отправится прямо в провайдер"
    );
    fireEvent.change(textarea, { target: { value: "nit: rename" } });
    fireEvent.click(screen.getByRole("button", { name: /Отправить/ }));

    await waitFor(() => {
      expect(submitReviewComment).toHaveBeenCalledWith(
        "intent-1",
        "b1",
        expect.objectContaining({
          content: "nit: rename",
          path: "src/app.ts",
          side: "right",
          line: 2,
          commit_sha: "head222",
          base_sha: "base111",
          start_sha: "start333"
        })
      );
    });
  });
});
