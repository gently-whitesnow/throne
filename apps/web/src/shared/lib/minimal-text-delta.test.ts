import { describe, expect, it } from "vitest";

import {
  computeMinimalTextDelta,
  type MinimalTextDelta
} from "./minimal-text-delta";

function expectDelta(
  delta: MinimalTextDelta | null
): asserts delta is MinimalTextDelta {
  if (delta === null) {
    throw new Error("expected a non-null delta");
  }
}

describe("computeMinimalTextDelta", () => {
  it("returns null when text is unchanged", () => {
    expect(computeMinimalTextDelta("abc", "abc")).toBeNull();
  });

  it("returns the changed substring without surrounding common context", () => {
    const original = "Lorem ipsum dolor sit amet, consectetur";
    const updated = "Lorem ipsum DOLOR sit amet, consectetur";
    expect(computeMinimalTextDelta(original, updated)).toEqual({
      oldText: "dolor",
      newText: "DOLOR"
    });
  });

  it("handles pure insertion at the end by anchoring on the last unique char", () => {
    const original = "abcz";
    const updated = "abcz!";
    expect(computeMinimalTextDelta(original, updated)).toEqual({
      oldText: "z",
      newText: "z!"
    });
  });

  it("expands tail anchor when the trailing character repeats elsewhere", () => {
    const original = "first line\nsecond line";
    const updated = "first line\nsecond line!";
    const delta = computeMinimalTextDelta(original, updated);
    expectDelta(delta);
    const occurrences = original.split(delta.oldText).length - 1;
    expect(occurrences).toBe(1);
    expect(updated.endsWith(delta.newText)).toBe(true);
    expect(delta.newText).toBe(delta.oldText + "!");
  });

  it("handles deletion by emitting empty new_text", () => {
    const original = "alpha beta gamma";
    const updated = "alpha  gamma";
    expect(computeMinimalTextDelta(original, updated)).toEqual({
      oldText: "beta",
      newText: ""
    });
  });

  it("expands context until old_text is unique in the document", () => {
    const original = "abcXabc";
    const updated = "abcYabc";
    const delta = computeMinimalTextDelta(original, updated);
    expectDelta(delta);
    const firstHit = original.indexOf(delta.oldText);
    expect(firstHit).toBeGreaterThanOrEqual(0);
    expect(original.includes(delta.oldText, firstHit + 1)).toBe(false);
    expect(delta.newText.replace("Y", "X")).toBe(delta.oldText);
  });

  it("falls back to the full document when nothing else is unique", () => {
    expect(computeMinimalTextDelta("aaa", "aaaa")).toEqual({
      oldText: "aaa",
      newText: "aaaa"
    });
  });

  it("handles full deletion to empty document", () => {
    expect(computeMinimalTextDelta("hello", "")).toEqual({
      oldText: "hello",
      newText: ""
    });
  });
});
