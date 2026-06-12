import { describe, expect, it } from "vitest";

import {
  ARCHIVE_STATUSES,
  FRIDGE_STATUS,
  INBOX_STATUSES,
  intentStatusMeta
} from "./types";

describe("intentStatusMeta", () => {
  it("covers every supported status", () => {
    expect(Object.keys(intentStatusMeta).sort()).toEqual([
      "awaiting_operator",
      "done",
      "draft",
      "fridge",
      "interview",
      "ready_for_review",
      "ready_for_work",
      "reject",
      "work"
    ]);
  });
});

describe("status groupings", () => {
  it("INBOX_STATUSES covers exactly ready_for_review and awaiting_operator", () => {
    expect([...INBOX_STATUSES].sort()).toEqual([
      "awaiting_operator",
      "ready_for_review"
    ]);
  });

  it("ARCHIVE_STATUSES covers exactly done and reject", () => {
    expect([...ARCHIVE_STATUSES].sort()).toEqual(["done", "reject"]);
  });

  it("FRIDGE_STATUS is a single value", () => {
    expect(FRIDGE_STATUS).toBe("fridge");
  });
});
