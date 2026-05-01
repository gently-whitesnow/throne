import { describe, expect, it } from "vitest";

import { intentStatusMeta } from "./types";

describe("intentStatusMeta", () => {
  it("covers every supported status", () => {
    expect(Object.keys(intentStatusMeta).sort()).toEqual([
      "active",
      "draft",
      "review"
    ]);
  });
});
