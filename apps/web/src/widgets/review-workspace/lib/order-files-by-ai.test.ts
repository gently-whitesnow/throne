import { describe, expect, it } from "vitest";

import type { PullRequestDiffFile } from "@/entities/review-workspace";

import { orderFilesByAi } from "./order-files-by-ai";

function file(path: string): PullRequestDiffFile {
  return { path, status: "modified", patch: "" };
}

describe("orderFilesByAi", () => {
  it("returns input untouched when file_order is empty/missing", () => {
    const natural = [file("a.ts"), file("b.ts")];
    expect(orderFilesByAi(natural, undefined).files).toEqual(natural);
    expect(orderFilesByAi(natural, []).files).toEqual(natural);
    expect(orderFilesByAi(natural, null).files).toEqual(natural);
  });

  it("places ranked files first in artifact order; rest follows natural order", () => {
    const natural = [file("a.ts"), file("b.ts"), file("c.ts"), file("d.ts")];
    const { files } = orderFilesByAi(natural, [
      { path: "c.ts", reason: "core", risk: "high" },
      { path: "a.ts", reason: "entry point" }
    ]);
    expect(files.map((f) => f.path)).toEqual(["c.ts", "a.ts", "b.ts", "d.ts"]);
  });

  it("ignores file_order entries that do not match a PR file", () => {
    const natural = [file("a.ts"), file("b.ts")];
    const { files, hints } = orderFilesByAi(natural, [
      { path: "ghost.ts", reason: "missing" },
      { path: "b.ts", reason: "real", risk: "low" }
    ]);
    expect(files.map((f) => f.path)).toEqual(["b.ts", "a.ts"]);
    expect(hints.has("ghost.ts")).toBe(false);
    expect(hints.get("b.ts")).toEqual({ reason: "real", risk: "low" });
  });

  it("deduplicates ranked paths, keeping the first occurrence", () => {
    const natural = [file("a.ts"), file("b.ts")];
    const { files } = orderFilesByAi(natural, [
      { path: "a.ts" },
      { path: "a.ts" },
      { path: "b.ts" }
    ]);
    expect(files.map((f) => f.path)).toEqual(["a.ts", "b.ts"]);
  });

  it("populates hints with nullable reason/risk for ranked files", () => {
    const natural = [file("a.ts"), file("b.ts")];
    const { hints } = orderFilesByAi(natural, [
      { path: "a.ts", reason: "core", risk: "high" },
      { path: "b.ts" }
    ]);
    expect(hints.get("a.ts")).toEqual({ reason: "core", risk: "high" });
    expect(hints.get("b.ts")).toEqual({ reason: null, risk: null });
  });
});
