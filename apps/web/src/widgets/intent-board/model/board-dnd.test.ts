import { describe, expect, it } from "vitest";

import {
  buildEntries,
  buildFlatIds,
  resolveCardMove,
  resolveClusterMove,
  type BoardEntry
} from "./board-dnd";
import type { ClusterInfo, ClustersResult } from "./useClusters";

function clusterInfo(id: string, members: string[]): ClusterInfo {
  return { clusterId: id, memberIds: members, commonTags: [] };
}

function clusterMap(...c: ClusterInfo[]): ClustersResult["clusters"] {
  const m = new Map<string, ClusterInfo>();
  for (const ci of c) m.set(ci.clusterId, ci);
  return m;
}

describe("buildEntries", () => {
  it("emits clusters at the position of their first member, drops repeats", () => {
    // order: x, a (member of A), b (member of A), y. byIntent: a→A, b→A.
    const byIntent = new Map([
      ["a", "A"],
      ["b", "A"]
    ]);
    const entries = buildEntries(["x", "a", "b", "y"], byIntent);
    expect(entries).toEqual([
      { kind: "single", anchorId: "x" },
      { kind: "cluster", anchorId: "A" },
      { kind: "single", anchorId: "y" }
    ]);
  });

  it("renders pure singletons when there are no clusters", () => {
    expect(buildEntries(["a", "b"], new Map())).toEqual([
      { kind: "single", anchorId: "a" },
      { kind: "single", anchorId: "b" }
    ]);
  });
});

describe("buildFlatIds", () => {
  it("expands cluster entries into their topo-ordered members", () => {
    const entries: BoardEntry[] = [
      { kind: "single", anchorId: "x" },
      { kind: "cluster", anchorId: "A" },
      { kind: "single", anchorId: "y" }
    ];
    const clusters = clusterMap(clusterInfo("A", ["a", "b", "c"]));
    expect(buildFlatIds(entries, clusters)).toEqual(["x", "a", "b", "c", "y"]);
  });
});

describe("resolveCardMove", () => {
  const flat = ["x", "a", "b", "c", "y"];
  it("places before the target", () => {
    expect(resolveCardMove("y", "b", "before", flat)).toEqual({
      movedId: "y",
      beforeId: "a",
      afterId: "b"
    });
  });
  it("places after the target", () => {
    expect(resolveCardMove("x", "b", "after", flat)).toEqual({
      movedId: "x",
      beforeId: "b",
      afterId: "c"
    });
  });
  it("skips the moved id if it sits in the resolved slot", () => {
    expect(resolveCardMove("a", "b", "before", flat)).toEqual({
      movedId: "a",
      beforeId: "x",
      afterId: "b"
    });
  });
});

describe("resolveClusterMove", () => {
  const moved = clusterInfo("A", ["a1", "a2"]);
  const clusters = clusterMap(moved, clusterInfo("B", ["b1", "b2"]));

  it("returns the right anchor pair when moving to the top", () => {
    const entries: BoardEntry[] = [
      { kind: "single", anchorId: "x" },
      { kind: "cluster", anchorId: "A" },
      { kind: "cluster", anchorId: "B" }
    ];
    const move = resolveClusterMove(moved, "x", "before", entries, clusters);
    expect(move).toEqual({
      clusterId: "A",
      memberIds: ["a1", "a2"],
      beforeId: null,
      afterId: "x"
    });
  });

  it("returns the right anchor pair when moving past another cluster", () => {
    const entries: BoardEntry[] = [
      { kind: "cluster", anchorId: "A" },
      { kind: "cluster", anchorId: "B" },
      { kind: "single", anchorId: "y" }
    ];
    const move = resolveClusterMove(moved, "B", "after", entries, clusters);
    expect(move).toEqual({
      clusterId: "A",
      memberIds: ["a1", "a2"],
      beforeId: "b2",
      afterId: "y"
    });
  });

  it("returns null for no-op self-drop", () => {
    const entries: BoardEntry[] = [
      { kind: "single", anchorId: "x" },
      { kind: "cluster", anchorId: "A" },
      { kind: "single", anchorId: "y" }
    ];
    expect(
      resolveClusterMove(moved, "x", "after", entries, clusters)
    ).toBeNull();
    expect(
      resolveClusterMove(moved, "y", "before", entries, clusters)
    ).toBeNull();
  });
});
