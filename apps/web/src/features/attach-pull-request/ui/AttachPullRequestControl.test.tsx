import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { RepositoryBinding } from "@/entities/repository-binding";

import { AttachPullRequestControl } from "./AttachPullRequestControl";

const attachIntentRepositoryPullRequest =
  vi.fn<
    (
      intentId: string,
      bindingId: string,
      pullRequestNumber: number
    ) => Promise<RepositoryBinding>
  >();

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  attachIntentRepositoryPullRequest: (
    intentId: string,
    bindingId: string,
    pullRequestNumber: number
  ) =>
    attachIntentRepositoryPullRequest(intentId, bindingId, pullRequestNumber),
  listIntentRepositories: vi.fn().mockResolvedValue([]),
  bindIntentRepository: vi.fn(),
  unbindIntentRepository: vi.fn(),
  searchGithubRepositories: vi.fn(),
  listMyGithubRepositories: vi.fn()
}));

vi.mock("@/shared/api", () => {
  class HttpError extends Error {
    public extensions = {};
    constructor(
      public status: number,
      public url: string,
      message: string
    ) {
      super(message);
    }
  }
  return { HttpError };
});

import { HttpError } from "@/shared/api";

function makeBinding(): RepositoryBinding {
  return {
    id: "b1",
    intent_id: "intent-1",
    provider: "github",
    owner: "octocat",
    repo: "hello-world",
    default_branch: "main",
    workspace_path: "/tmp/throne/intent-1/b1",
    clone_status: "ready",
    pull_request_number: 7,
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z"
  };
}

describe("AttachPullRequestControl", () => {
  beforeEach(() => {
    attachIntentRepositoryPullRequest.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит кнопку «Привязать PR»", () => {
    renderWithQuery(
      <AttachPullRequestControl intentId="intent-1" bindingId="b1" />,
      { withBridge: false }
    );
    expect(screen.getByTestId("attach-pr-open-b1").textContent).toMatch(
      /Привязать PR/
    );
  });

  it("ввод номера и submit вызывают api и инвалидируют список", async () => {
    attachIntentRepositoryPullRequest.mockResolvedValue(makeBinding());
    const { queryClient } = renderWithQuery(
      <AttachPullRequestControl intentId="intent-1" bindingId="b1" />,
      { withBridge: false }
    );
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");

    fireEvent.click(screen.getByTestId("attach-pr-open-b1"));
    fireEvent.change(screen.getByTestId("attach-pr-input-b1"), {
      target: { value: "42" }
    });
    fireEvent.click(screen.getByTestId("attach-pr-submit-b1"));

    await waitFor(() => {
      expect(attachIntentRepositoryPullRequest).toHaveBeenCalledWith(
        "intent-1",
        "b1",
        42
      );
    });
    await waitFor(() => {
      expect(invalidate).toHaveBeenCalledWith(
        expect.objectContaining({
          queryKey: ["intent-repositories", "intent-1"]
        })
      );
    });
  });

  it("submit заблокирован при пустом / невалидном вводе", () => {
    renderWithQuery(
      <AttachPullRequestControl intentId="intent-1" bindingId="b1" />,
      { withBridge: false }
    );
    fireEvent.click(screen.getByTestId("attach-pr-open-b1"));
    const submit = screen.getByTestId("attach-pr-submit-b1");
    expect(submit.hasAttribute("disabled")).toBe(true);

    fireEvent.change(screen.getByTestId("attach-pr-input-b1"), {
      target: { value: "0" }
    });
    expect(submit.hasAttribute("disabled")).toBe(true);
  });

  it("409 показывает inline-ошибку «уже привязан»", async () => {
    attachIntentRepositoryPullRequest.mockRejectedValue(
      new HttpError(409, "/x", "conflict")
    );
    renderWithQuery(
      <AttachPullRequestControl intentId="intent-1" bindingId="b1" />,
      { withBridge: false }
    );
    fireEvent.click(screen.getByTestId("attach-pr-open-b1"));
    fireEvent.change(screen.getByTestId("attach-pr-input-b1"), {
      target: { value: "42" }
    });
    fireEvent.click(screen.getByTestId("attach-pr-submit-b1"));

    await waitFor(() => {
      expect(screen.getByTestId("attach-pr-error-b1").textContent).toMatch(
        /уже привязан/
      );
    });
  });
});
