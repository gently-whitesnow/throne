import {
  cleanup,
  fireEvent,
  screen,
  waitFor,
  within
} from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { PullRequestComment } from "@/entities/pull-request-comment";
import type { RepositoryBinding } from "@/entities/repository-binding";
import type {
  PullRequestCommit,
  PullRequestDiff
} from "@/entities/review-workspace";

import { ReviewWorkspaceEntry } from "./ReviewWorkspaceEntry";
import { ReviewWorkspaceRoute } from "./ReviewWorkspaceRoute";

const listIntentRepositories =
  vi.fn<(intentId: string) => Promise<RepositoryBinding[]>>();
const getReviewDiff = vi.fn<() => Promise<PullRequestDiff>>();
const listReviewCommits = vi.fn<() => Promise<PullRequestCommit[]>>();
const listPullRequestComments = vi.fn<() => Promise<PullRequestComment[]>>();
const deletePullRequestComment =
  vi.fn<
    (
      intentId: string,
      bindingId: string,
      commentId: string,
      threadId?: string | null
    ) => Promise<void>
  >();
const updateReviewThread =
  vi.fn<
    (
      intentId: string,
      bindingId: string,
      threadId: string,
      resolved: boolean
    ) => Promise<{ thread_id: string; resolved: boolean }>
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
  getReviewPullRequest: () => Promise.resolve({ number: 1, state: "open" }),
  submitReviewComment: vi.fn()
}));

vi.mock("@/entities/pull-request-comment/api/pr-comments-api", () => ({
  listPullRequestComments: () => listPullRequestComments(),
  syncPullRequest: () => Promise.resolve({ binding_id: "b1" }),
  deletePullRequestComment: (
    intentId: string,
    bindingId: string,
    commentId: string,
    threadId?: string | null
  ) => deletePullRequestComment(intentId, bindingId, commentId, threadId),
  updateReviewThread: (
    intentId: string,
    bindingId: string,
    threadId: string,
    resolved: boolean
  ) => updateReviewThread(intentId, bindingId, threadId, resolved)
}));

vi.mock("@/shared/realtime", () => ({
  useRealtimeEvent: () => undefined
}));

function makeBinding(): RepositoryBinding {
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
    updated_at: "2026-05-20T10:00:00Z"
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
    },
    {
      path: "src/other.ts",
      status: "modified",
      patch: ["@@ -1,1 +1,1 @@", "-foo", "+bar"].join("\n")
    }
  ]
};

function makeComment(
  overrides: Partial<PullRequestComment>
): PullRequestComment {
  return {
    id: "c1",
    binding_id: "b1",
    author_login: "reviewer",
    body: "default body",
    created_at: "2026-05-22T10:00:00Z",
    ...overrides
  };
}

function renderWorkspace(comments: PullRequestComment[]) {
  listPullRequestComments.mockResolvedValue(comments);
  return renderWithQuery(
    <MemoryRouter
      initialEntries={["/intents/intent-1/review/b1?file=src%2Fapp.ts"]}
    >
      <Routes>
        <Route
          path="/intents/:id"
          element={
            <>
              <ReviewWorkspaceEntry intentId="intent-1" />
              <Outlet />
            </>
          }
        >
          <Route path="review/:bindingId" element={<ReviewWorkspaceRoute />} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe("Review workspace — inline comments", () => {
  beforeEach(() => {
    listIntentRepositories.mockReset().mockResolvedValue([makeBinding()]);
    getReviewDiff.mockReset().mockResolvedValue(DIFF);
    listReviewCommits.mockReset().mockResolvedValue([]);
    listPullRequestComments.mockReset().mockResolvedValue([]);
    deletePullRequestComment.mockReset().mockResolvedValue(undefined);
    updateReviewThread
      .mockReset()
      .mockResolvedValue({ thread_id: "t1", resolved: true });
  });

  afterEach(() => {
    cleanup();
  });

  it("renders an existing comment inline under its matched diff row", async () => {
    renderWorkspace([
      makeComment({
        id: "inline-1",
        path: "src/app.ts",
        side: "right",
        line: 2,
        body: "inline note on new line"
      })
    ]);

    await screen.findByRole("dialog", { name: "Review workspace" });
    // The +new row is at newLine 2 (right side); the comment renders right after.
    const bodies = await screen.findAllByText("inline note on new line");
    // Rendered both inline (under the diff row) and in the right rail.
    expect(bodies.some((el) => el.closest(".diff-hl") !== null)).toBe(true);
    // The inline copy sits immediately after the matched "+new" row.
    const addedRow = screen.getByText("new").closest("div.group");
    const thread = addedRow?.nextElementSibling as HTMLElement;
    expect(within(thread).getByText("inline note on new line")).toBeTruthy();
  });

  it("clicking a rail comment switches to its file", async () => {
    renderWorkspace([
      makeComment({
        id: "rail-1",
        path: "src/other.ts",
        side: "right",
        line: 1,
        body: "comment on other file"
      })
    ]);

    await screen.findByRole("dialog", { name: "Review workspace" });
    // Header shows the initially active file.
    await screen.findByText("src/app.ts");

    // Right rail opens on the "Описание" tab; switch to comments first.
    fireEvent.click(screen.getByRole("tab", { name: /Комментарии/ }));

    // Click the rail comment's author header (the jump affordance).
    const railBody = await screen.findByText("comment on other file");
    const card = railBody.closest("div") as HTMLElement;
    fireEvent.click(within(card).getByText("reviewer"));

    // Diff header now shows the comment's file.
    await waitFor(() => {
      expect(screen.getAllByText("src/other.ts").length).toBeGreaterThan(0);
    });
  });

  it("collapses a resolved comment and expands it on click", async () => {
    renderWorkspace([
      makeComment({
        id: "resolved-1",
        path: "src/app.ts",
        side: "right",
        line: 2,
        thread_id: "t1",
        resolved: true,
        body: "hidden until expanded"
      })
    ]);

    await screen.findByRole("dialog", { name: "Review workspace" });
    // Collapsed: body not shown, resolved chip present.
    expect(screen.queryByText("hidden until expanded")).toBeNull();
    const chips = await screen.findAllByText("Решено");
    expect(chips.length).toBeGreaterThan(0);

    fireEvent.click(
      screen.getAllByRole("button", { name: "Развернуть комментарий" })[0]
    );
    expect(await screen.findByText("hidden until expanded")).toBeTruthy();
  });

  it("deletes a comment via the API after confirm", async () => {
    renderWorkspace([
      makeComment({
        id: "del-1",
        path: "src/app.ts",
        side: "right",
        line: 2,
        thread_id: "t9",
        body: "to delete"
      })
    ]);

    await screen.findByRole("dialog", { name: "Review workspace" });
    // Unresolved → expanded; delete affordance is visible (use the inline one).
    fireEvent.click(screen.getAllByRole("button", { name: /Удалить/ })[0]);
    fireEvent.click(screen.getAllByRole("button", { name: "Да" })[0]);

    await waitFor(() => {
      expect(deletePullRequestComment).toHaveBeenCalledWith(
        "intent-1",
        "b1",
        "del-1",
        "t9"
      );
    });
  });

  it("resolves a thread via the API", async () => {
    renderWorkspace([
      makeComment({
        id: "res-1",
        path: "src/app.ts",
        side: "right",
        line: 2,
        thread_id: "t5",
        resolved: false,
        body: "please resolve"
      })
    ]);

    await screen.findByRole("dialog", { name: "Review workspace" });
    fireEvent.click(screen.getAllByRole("button", { name: /Решить/ })[0]);

    await waitFor(() => {
      expect(updateReviewThread).toHaveBeenCalledWith(
        "intent-1",
        "b1",
        "t5",
        true
      );
    });
  });
});
