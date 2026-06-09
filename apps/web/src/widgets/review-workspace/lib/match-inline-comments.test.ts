import { describe, expect, it } from "vitest";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import type { PullRequestDiffFile } from "@/entities/review-workspace";

import { indexInlineComments, rowAnchorKey } from "./match-inline-comments";

const FILE: PullRequestDiffFile = {
  path: "src/app.ts",
  status: "modified",
  patch: ""
};

function makeComment(
  overrides: Partial<PullRequestComment>
): PullRequestComment {
  return {
    id: "c1",
    binding_id: "b1",
    author_login: "me",
    body: "nit",
    created_at: "2026-05-22T10:00:00Z",
    ...overrides
  };
}

describe("rowAnchorKey", () => {
  it("anchors del rows to left/oldLine and others to right/newLine", () => {
    expect(
      rowAnchorKey({ kind: "del", oldLine: 3, newLine: null, content: "x" })
    ).toBe("left:3");
    expect(
      rowAnchorKey({ kind: "add", oldLine: null, newLine: 5, content: "x" })
    ).toBe("right:5");
    expect(
      rowAnchorKey({ kind: "context", oldLine: 2, newLine: 2, content: "x" })
    ).toBe("right:2");
  });
});

describe("indexInlineComments", () => {
  it("buckets anchorable comments and drops unanchored / off-path ones", () => {
    const anchored = makeComment({
      id: "a",
      path: "src/app.ts",
      side: "right",
      line: 5
    });
    const sameRow = makeComment({
      id: "a2",
      path: "src/app.ts",
      side: "right",
      line: 5
    });
    const offPath = makeComment({
      id: "b",
      path: "src/other.ts",
      side: "right",
      line: 5
    });
    const noAnchor = makeComment({ id: "c", path: "src/app.ts" });

    const index = indexInlineComments(FILE, [
      anchored,
      sameRow,
      offPath,
      noAnchor
    ]);

    expect([...index.keys()]).toEqual(["right:5"]);
    expect(index.get("right:5")?.map((c) => c.id)).toEqual(["a", "a2"]);
  });
});
