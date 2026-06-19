import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { PullRequestDiffFile } from "@/entities/review-workspace";

import { ReviewDiffViewer } from "./ReviewDiffViewer";

// The viewer renders one ReviewDiffRow per line; the rows pull in comments,
// composer and syntax highlighting we don't care about here, so stub them out.
vi.mock("./ReviewDiffRow", () => ({
  ReviewDiffRow: () => null
}));

afterEach(cleanup);

const FILE: PullRequestDiffFile = {
  path: "src/app.ts",
  previous_path: null,
  status: "modified",
  patch: "@@ -1,1 +1,1 @@\n-old\n+new\n"
};

const SHAS = { base_sha: "b", commit_sha: "c", start_sha: "s" };

function renderViewer(reason: string | null) {
  return render(
    <ReviewDiffViewer
      file={FILE}
      shas={SHAS}
      intentId="i"
      bindingId="bind"
      comments={[]}
      commentActions={{ onDelete: vi.fn(), onToggleResolved: vi.fn() }}
      scrollTarget={null}
      reason={reason}
      onSubmitted={vi.fn()}
    />
  );
}

describe("ReviewDiffViewer header", () => {
  it("показывает AI-подсказку инлайн рядом с кнопкой", () => {
    renderViewer("Core terminal config; review the stdio key handling closely.");
    // getByText/getByRole throw when the node is missing, so finding both proves
    // the reason and the button coexist in the header.
    expect(
      screen.getByText(/Core terminal config; review the stdio key handling/)
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: /Весь файл/ })).toBeTruthy();
  });

  it("без подсказки кнопка остаётся, текст не рендерится", () => {
    const { container } = renderViewer(null);
    expect(screen.getByRole("button", { name: /Весь файл/ })).toBeTruthy();
    expect(container.querySelector("p")).toBeNull();
  });
});
