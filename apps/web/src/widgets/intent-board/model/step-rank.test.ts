import { describe, expect, it } from "vitest";

import type { IntentLinksSummaryEntry } from "@/entities/intent";

import { computeStepRanks } from "./step-rank";

function entry(
  intentId: string,
  blockerIds: string[]
): IntentLinksSummaryEntry {
  return {
    intent_id: intentId,
    blocked_by: blockerIds.map((id) => peer(id)),
    blocks: [],
    linked_from: [],
    linked_to: []
  };
}

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

describe("computeStepRanks", () => {
  it("returns 1 for intents with no blockers", () => {
    const ranks = computeStepRanks(["a", "b"], new Map());
    expect(ranks.get("a")).toBe(1);
    expect(ranks.get("b")).toBe(1);
  });

  it("propagates depth through a linear chain", () => {
    // a → b → c (a blocks b, b blocks c)
    const summary = new Map([
      ["b", entry("b", ["a"])],
      ["c", entry("c", ["b"])]
    ]);
    const ranks = computeStepRanks(["a", "b", "c"], summary);
    expect(ranks.get("a")).toBe(1);
    expect(ranks.get("b")).toBe(2);
    expect(ranks.get("c")).toBe(3);
  });

  it("uses the longest path when multiple blockers fan in", () => {
    // a → c, b → c → d, where a is rank 1 and b depends on a (rank 2).
    const summary = new Map([
      ["b", entry("b", ["a"])],
      ["c", entry("c", ["a", "b"])],
      ["d", entry("d", ["c"])]
    ]);
    const ranks = computeStepRanks(["a", "b", "c", "d"], summary);
    expect(ranks.get("c")).toBe(3); // 1 + max(rank(a)=1, rank(b)=2) = 3
    expect(ranks.get("d")).toBe(4);
  });

  it("ignores blockers that are not in the visible set", () => {
    // off-page blocker doesn't push the rank up.
    const summary = new Map([["b", entry("b", ["off-page"])]]);
    const ranks = computeStepRanks(["b"], summary);
    expect(ranks.get("b")).toBe(1);
  });

  it("terminates on cycles with finite ranks", () => {
    // a ↔ b cycle: each blocks the other. Should not loop forever; rank
    // collapses to 1 for the first node visited (cycle edge treated as «no
    // constraint») and 2 for the second.
    const summary = new Map([
      ["a", entry("a", ["b"])],
      ["b", entry("b", ["a"])]
    ]);
    const ranks = computeStepRanks(["a", "b"], summary);
    expect(ranks.get("a")).toBeGreaterThanOrEqual(1);
    expect(ranks.get("b")).toBeGreaterThanOrEqual(1);
  });
});
