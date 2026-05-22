import { describe, expect, it } from "vitest";

import { buildSideBySideDiff } from "./build-side-by-side-diff";

describe("buildSideBySideDiff", () => {
  it("returns context-only rows when texts are identical", () => {
    const rows = buildSideBySideDiff("a\nb\nc", "a\nb\nc");
    expect(rows).toEqual([
      {
        left: { kind: "context", text: "a" },
        right: { kind: "context", text: "a" }
      },
      {
        left: { kind: "context", text: "b" },
        right: { kind: "context", text: "b" }
      },
      {
        left: { kind: "context", text: "c" },
        right: { kind: "context", text: "c" }
      }
    ]);
  });

  it("pairs removed and added lines into modification rows", () => {
    const rows = buildSideBySideDiff("a\nold\nc", "a\nnew\nc");
    expect(rows).toEqual([
      {
        left: { kind: "context", text: "a" },
        right: { kind: "context", text: "a" }
      },
      {
        left: { kind: "removed", text: "old" },
        right: { kind: "added", text: "new" }
      },
      {
        left: { kind: "context", text: "c" },
        right: { kind: "context", text: "c" }
      }
    ]);
  });

  it("places empty placeholder on the right for a pure deletion", () => {
    const rows = buildSideBySideDiff("a\ngone\nc", "a\nc");
    expect(rows).toEqual([
      {
        left: { kind: "context", text: "a" },
        right: { kind: "context", text: "a" }
      },
      {
        left: { kind: "removed", text: "gone" },
        right: { kind: "empty" }
      },
      {
        left: { kind: "context", text: "c" },
        right: { kind: "context", text: "c" }
      }
    ]);
  });

  it("places empty placeholder on the left for a pure insertion", () => {
    const rows = buildSideBySideDiff("a\nc", "a\nfresh\nc");
    expect(rows).toEqual([
      {
        left: { kind: "context", text: "a" },
        right: { kind: "context", text: "a" }
      },
      {
        left: { kind: "empty" },
        right: { kind: "added", text: "fresh" }
      },
      {
        left: { kind: "context", text: "c" },
        right: { kind: "context", text: "c" }
      }
    ]);
  });

  it("pads the shorter side with empty placeholders for asymmetric edits", () => {
    const rows = buildSideBySideDiff("x\nold1\nold2\ny", "x\nnew1\ny");
    expect(rows).toEqual([
      {
        left: { kind: "context", text: "x" },
        right: { kind: "context", text: "x" }
      },
      {
        left: { kind: "removed", text: "old1" },
        right: { kind: "added", text: "new1" }
      },
      {
        left: { kind: "removed", text: "old2" },
        right: { kind: "empty" }
      },
      {
        left: { kind: "context", text: "y" },
        right: { kind: "context", text: "y" }
      }
    ]);
  });

  it("handles empty current text as pure insertion", () => {
    const rows = buildSideBySideDiff("", "hello");
    expect(rows).toEqual([
      {
        left: { kind: "empty" },
        right: { kind: "added", text: "hello" }
      }
    ]);
  });

  it("returns an empty array when both texts are empty", () => {
    expect(buildSideBySideDiff("", "")).toEqual([]);
  });
});
