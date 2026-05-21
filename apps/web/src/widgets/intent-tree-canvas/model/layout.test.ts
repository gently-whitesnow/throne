import { describe, expect, it } from "vitest";

import {
  CARD_H,
  CARD_W,
  COL_GAP,
  ROW_GAP,
  layoutTree,
  type LayoutNode
} from "./layout";

function makeNodes(
  spec: readonly (readonly [string, readonly string[]])[]
): LayoutNode[] {
  return spec.map(([id, parents]) => ({ id, parents }));
}

describe("layoutTree", () => {
  it("returns empty result for empty input", () => {
    const { pos, bounds } = layoutTree([]);
    expect(pos.size).toBe(0);
    expect(bounds).toEqual({ w: 0, h: 0, cols: 0 });
  });

  it("lays out a linear chain across monotone columns", () => {
    const nodes = makeNodes([
      ["a", []],
      ["b", ["a"]],
      ["c", ["b"]]
    ]);
    const { pos, bounds } = layoutTree(nodes);
    const xA = pos.get("a")?.x;
    const xB = pos.get("b")?.x;
    const xC = pos.get("c")?.x;
    expect(xA).toBe(0);
    expect(xB).toBe(CARD_W + COL_GAP);
    expect(xC).toBe(2 * (CARD_W + COL_GAP));
    expect(bounds.cols).toBe(3);
    expect(bounds.w).toBe(2 * (CARD_W + COL_GAP) + CARD_W);
  });

  it("centers a child between its two parents (Y-axis)", () => {
    const nodes = makeNodes([
      ["a", []],
      ["b", []],
      ["c", ["a", "b"]]
    ]);
    const { pos } = layoutTree(nodes);
    const yA = pos.get("a")?.y ?? 0;
    const yB = pos.get("b")?.y ?? 0;
    const yC = pos.get("c")?.y ?? 0;
    expect(yA).toBe(0);
    expect(yB).toBe(CARD_H + ROW_GAP);
    // After smoothing the child snaps to the centroid between the parents.
    expect(yC).toBeCloseTo((yA + yB) / 2, 5);
  });

  it("handles a 3-way cycle without infinite recursion", () => {
    const nodes = makeNodes([
      ["a", ["c"]],
      ["b", ["a"]],
      ["c", ["b"]]
    ]);
    // Should not throw and should assign every node a position.
    const { pos } = layoutTree(nodes);
    expect(pos.size).toBe(3);
    expect(pos.get("a")).toBeDefined();
    expect(pos.get("b")).toBeDefined();
    expect(pos.get("c")).toBeDefined();
  });

  it("ignores self-loops", () => {
    const nodes = makeNodes([["solo", ["solo"]]]);
    const { pos, bounds } = layoutTree(nodes);
    expect(pos.get("solo")).toEqual({ x: 0, y: 0 });
    expect(bounds.cols).toBe(1);
  });

  it("is deterministic across repeated calls with the same input", () => {
    const nodes = makeNodes([
      ["root1", []],
      ["root2", []],
      ["mid", ["root1"]],
      ["leaf", ["mid", "root2"]]
    ]);
    const a = layoutTree(nodes);
    const b = layoutTree(nodes);
    expect([...a.pos.entries()].sort()).toEqual([...b.pos.entries()].sort());
    expect(a.bounds).toEqual(b.bounds);
  });

  it("drops references to parents not present in the input set", () => {
    const nodes = makeNodes([["only", ["missing"]]]);
    const { pos } = layoutTree(nodes);
    // "missing" has no node, so "only" should be treated as a root (col 0).
    expect(pos.get("only")).toEqual({ x: 0, y: 0 });
  });
});
