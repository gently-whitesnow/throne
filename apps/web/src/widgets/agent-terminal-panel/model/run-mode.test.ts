import { describe, expect, it } from "vitest";

import type { IntentStatus } from "@/entities/intent";

import { defaultRunModeForStatus } from "./types";

describe("defaultRunModeForStatus", () => {
  it("черновик стартует в режиме интервью", () => {
    expect(defaultRunModeForStatus("draft")).toBe("interview");
  });

  it("готовый к работе стартует в режиме работы", () => {
    expect(defaultRunModeForStatus("ready_for_work")).toBe("work");
  });

  it.each<IntentStatus>([
    "interview",
    "work",
    "ready_for_review",
    "needs_help",
    "done",
    "reject",
    "fridge"
  ])("прочие статусы (%s) стартуют в свободном режиме", (status) => {
    expect(defaultRunModeForStatus(status)).toBe("free");
  });
});
