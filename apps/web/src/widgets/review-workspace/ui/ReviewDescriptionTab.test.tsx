import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { PullRequestHeader } from "@/entities/review-workspace";

import { ReviewDescriptionTab } from "./ReviewDescriptionTab";

const getReviewPullRequest = vi.fn<() => Promise<PullRequestHeader>>();

vi.mock("@/entities/review-workspace/api/review-api", () => ({
  getReviewPullRequest: () => getReviewPullRequest(),
  getReviewDiff: vi.fn(),
  listReviewCommits: vi.fn(),
  submitReviewComment: vi.fn()
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const header: PullRequestHeader = {
  number: 7,
  title: "Add description tab",
  state: "open",
  author_login: "octocat",
  author_avatar_url: null,
  head_ref: "feat/desc",
  base_ref: "main",
  html_url: "https://github.com/o/r/pull/7",
  body: "# Why\n\nContext here."
};

it("рендерит шапку PR с заголовком, автором, ветками и markdown-телом", async () => {
  getReviewPullRequest.mockResolvedValue(header);

  renderWithQuery(<ReviewDescriptionTab intentId="i1" bindingId="b1" />, {
    withBridge: false
  });

  expect(await screen.findByText("Add description tab")).toBeTruthy();
  expect(screen.getByText("octocat")).toBeTruthy();
  expect(screen.getByText(/feat\/desc → main/)).toBeTruthy();
  expect(screen.getByText("Открыт")).toBeTruthy();
  expect(screen.getByRole("heading", { name: "Why" })).toBeTruthy();
  expect(
    screen.getByRole("link", { name: /Открыть/ }).getAttribute("href")
  ).toBe(header.html_url);
});

it("кнопка «Обновить» перезапрашивает шапку", async () => {
  getReviewPullRequest.mockResolvedValue(header);

  renderWithQuery(<ReviewDescriptionTab intentId="i1" bindingId="b1" />, {
    withBridge: false
  });

  await screen.findByText("Add description tab");
  expect(getReviewPullRequest).toHaveBeenCalledTimes(1);

  fireEvent.click(screen.getByRole("button", { name: "Обновить описание" }));

  await waitFor(() => {
    expect(getReviewPullRequest).toHaveBeenCalledTimes(2);
  });
});
