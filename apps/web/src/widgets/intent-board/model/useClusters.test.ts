import { describe, expect, it } from "vitest";

import type { IntentLinksSummaryEntry } from "@/entities/intent";

import { computeClusters } from "./useClusters";

function peer(id: string) {
  return {
    id,
    status: "draft" as const,
    current_version: 1,
    sort_key: "V",
    text_short: id,
    tags: []
  };
}

function entry(
  intentId: string,
  edges: Partial<{
    blocked_by: string[];
    derived_from: string[];
    source_of: string[];
    relates: string[];
  }>
): IntentLinksSummaryEntry {
  return {
    intent_id: intentId,
    blocked_by: (edges.blocked_by ?? []).map(peer),
    derived_from: (edges.derived_from ?? []).map(peer),
    source_of: (edges.source_of ?? []).map(peer),
    relates: (edges.relates ?? []).map(peer)
  };
}

function items(...ids: string[]) {
  return ids.map((id) => ({ id, tagNames: [] as string[] }));
}

describe("computeClusters", () => {
  it("treats isolated intents as singletons (not in result)", () => {
    const { byIntent, clusters } = computeClusters(items("a", "b"), new Map());
    expect(byIntent.size).toBe(0);
    expect(clusters.size).toBe(0);
  });

  it("unions intents connected by any link type", () => {
    const summary = new Map([
      ["b", entry("b", { blocked_by: ["a"] })],
      ["c", entry("c", { derived_from: ["b"] })],
      ["d", entry("d", { relates: ["c"] })]
    ]);
    const { byIntent, clusters } = computeClusters(
      items("a", "b", "c", "d"),
      summary
    );
    expect(clusters.size).toBe(1);
    const cluster = [...clusters.values()][0];
    expect(cluster.clusterId).toBe("a");
    expect(new Set(cluster.memberIds)).toEqual(new Set(["a", "b", "c", "d"]));
    expect(byIntent.get("a")).toBe("a");
    expect(byIntent.get("d")).toBe("a");
  });

  it("keeps unrelated intents in separate clusters / singletons", () => {
    const summary = new Map([
      ["b", entry("b", { blocked_by: ["a"] })],
      ["d", entry("d", { blocked_by: ["c"] })]
    ]);
    const { clusters, byIntent } = computeClusters(
      items("a", "b", "c", "d", "e"),
      summary
    );
    expect(clusters.size).toBe(2);
    expect(byIntent.has("e")).toBe(false);
    expect(byIntent.get("a")).toBe("a");
    expect(byIntent.get("c")).toBe("c");
  });

  it("orders members by blocks/derived precedence within a cluster", () => {
    // a blocks b, b blocks c — expected order [a, b, c]
    const summary = new Map([
      ["b", entry("b", { blocked_by: ["a"] })],
      ["c", entry("c", { blocked_by: ["b"] })]
    ]);
    const { clusters } = computeClusters(items("c", "a", "b"), summary);
    const cluster = [...clusters.values()][0];
    expect(cluster.memberIds).toEqual(["a", "b", "c"]);
  });

  it("falls back to id order on cycles without infinite-looping", () => {
    const summary = new Map([
      ["a", entry("a", { blocked_by: ["b"] })],
      ["b", entry("b", { blocked_by: ["a"] })]
    ]);
    const { clusters } = computeClusters(items("a", "b"), summary);
    const cluster = [...clusters.values()][0];
    expect(cluster.memberIds).toEqual(["a", "b"]);
  });

  it("ignores off-screen peers when computing connectivity", () => {
    // a links off-screen X; b is alone visibly. No cluster expected.
    const summary = new Map([
      ["a", entry("a", { relates: ["off"] })],
      ["b", entry("b", { relates: ["off"] })]
    ]);
    const { clusters } = computeClusters(items("a", "b"), summary);
    expect(clusters.size).toBe(0);
  });

  it("computes intersection of member tags for cluster header", () => {
    const summary = new Map([
      ["b", entry("b", { blocked_by: ["a"] })],
      ["c", entry("c", { blocked_by: ["b"] })]
    ]);
    const data = [
      { id: "a", tagNames: ["throne", "ui"] },
      { id: "b", tagNames: ["throne", "ui"] },
      { id: "c", tagNames: ["throne"] }
    ];
    const { clusters } = computeClusters(data, summary);
    const cluster = [...clusters.values()][0];
    expect(cluster.commonTags).toEqual(["throne"]);
  });
});
