import { describe, expect, it } from "vitest";

import { compareSortKeys } from "./sortKey";

describe("compareSortKeys", () => {
  // Regression: localeCompare case-folds 'V' and 's' under most locales so that
  // "s".localeCompare("V") < 0 — which inverts our list vs. the server's ordinal
  // sort and produces 500 errors when DnD pivots cross case boundaries.
  it("uses ordinal (byte-wise) comparison, NOT locale-aware", () => {
    // 'V' (U+0056 = 86) is byte-wise < 's' (U+0073 = 115).
    expect(compareSortKeys("V", "s")).toBeLessThan(0);
    expect(compareSortKeys("s", "V")).toBeGreaterThan(0);

    // Sanity check that we did not accidentally implement localeCompare.
    expect("s".localeCompare("V")).toBeLessThan(0);
  });

  it("orders the migration's GenerateAscending sequence the same as the server", () => {
    // Backend FractionalIndex.GenerateAscending starts: V, k, s, w, ...
    // (see Throne.Domain.Intents.FractionalIndex). Sorting them ASC must match
    // that emission order, otherwise rendered neighbours disagree with the DB.
    const generated = ["V", "k", "s", "w"];
    const shuffled = [...generated].reverse();
    expect([...shuffled].sort(compareSortKeys)).toEqual(generated);
  });

  it("returns 0 for equal keys", () => {
    expect(compareSortKeys("Vk", "Vk")).toBe(0);
  });
});
