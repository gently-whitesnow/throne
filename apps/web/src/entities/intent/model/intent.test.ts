import { describe, expect, it } from "vitest";

import { intentStatusMeta } from "./types";

describe("intentStatusMeta", () => {
  it("covers every supported status", () => {
    expect(Object.keys(intentStatusMeta).sort()).toEqual([
      "done",
      "draft",
      "interview",
      "ready_for_review",
      "ready_for_work",
      "reject",
      "work"
    ]);
  });
});
