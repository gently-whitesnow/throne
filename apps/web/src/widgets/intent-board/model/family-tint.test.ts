import { describe, expect, it } from "vitest";

import type { IntentLinksSummaryEntry } from "@/entities/intent";

import { computeFamilyTints } from "./family-tint";

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
  parents: string[] = []
): IntentLinksSummaryEntry {
  return {
    intent_id: intentId,
    blocked_by: [],
    derived_from: parents.map((id) => peer(id)),
    source_of: [],
    relates: []
  };
}

describe("computeFamilyTints", () => {
  it("does not tint singleton families", () => {
    const tints = computeFamilyTints(["a", "b"], new Map());
    expect(tints.size).toBe(0);
  });

  it("tints two siblings sharing an off-screen parent with the same colour", () => {
    // a и b — дети off-screen P; самого P в visibleIds нет.
    const summary = new Map([
      ["a", entry("a", ["P"])],
      ["b", entry("b", ["P"])]
    ]);
    const tints = computeFamilyTints(["a", "b"], summary);
    expect(tints.get("a")).toBeDefined();
    expect(tints.get("a")).toEqual(tints.get("b"));
  });

  it("groups parent and its visible children into one family", () => {
    // P — родитель в кадре; a, b — его дети.
    const summary = new Map([
      ["a", entry("a", ["P"])],
      ["b", entry("b", ["P"])],
      ["P", entry("P", [])]
    ]);
    const tints = computeFamilyTints(["P", "a", "b"], summary);
    const colour = tints.get("P");
    expect(colour).toBeDefined();
    expect(tints.get("a")).toBe(colour);
    expect(tints.get("b")).toBe(colour);
  });

  it("walks transitively through visible ancestors", () => {
    // grandchild → child → P (P off-screen). Все в одной семье.
    const summary = new Map([
      ["child", entry("child", ["P"])],
      ["grandchild", entry("grandchild", ["child"])]
    ]);
    const tints = computeFamilyTints(["child", "grandchild"], summary);
    expect(tints.get("child")).toBe(tints.get("grandchild"));
  });

  it("assigns different colours to independent families", () => {
    const summary = new Map([
      ["a1", entry("a1", ["A"])],
      ["a2", entry("a2", ["A"])],
      ["b1", entry("b1", ["B"])],
      ["b2", entry("b2", ["B"])]
    ]);
    const tints = computeFamilyTints(["a1", "a2", "b1", "b2"], summary);
    expect(tints.get("a1")).toBe(tints.get("a2"));
    expect(tints.get("b1")).toBe(tints.get("b2"));
    expect(tints.get("a1")).not.toBe(tints.get("b1"));
  });

  it("tolerates derived_from cycles", () => {
    const summary = new Map([
      ["x", entry("x", ["y"])],
      ["y", entry("y", ["x"])]
    ]);
    // Не должно зависнуть; просто оба попадают в одну семью.
    const tints = computeFamilyTints(["x", "y"], summary);
    expect(tints.get("x")).toBeDefined();
    expect(tints.get("x")).toBe(tints.get("y"));
  });
});
