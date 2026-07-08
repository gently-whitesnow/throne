import { describe, expect, it } from "vitest";

import type { IntentStatus } from "@/entities/intent";

import {
  RUN_MODE_LABEL,
  TERMINAL_RUN_MODES,
  defaultRunModeForStatus
} from "./types";

describe("TERMINAL_RUN_MODES", () => {
  it("режим рефлексии доступен в дропдауне как «Рефлексия»", () => {
    expect(TERMINAL_RUN_MODES).toContain("dream");
    expect(RUN_MODE_LABEL.dream).toBe("Рефлексия");
  });

  it("дефолтным режим рефлексии не делается ни в одном статусе", () => {
    const statuses: IntentStatus[] = [
      "draft",
      "interview",
      "work",
      "ready_for_work",
      "awaiting_operator",
      "done",
      "reject",
      "fridge"
    ];
    for (const status of statuses) {
      expect(defaultRunModeForStatus(status)).not.toBe("dream");
    }
  });
});

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
    "awaiting_operator",
    "done",
    "reject",
    "fridge"
  ])("прочие статусы (%s) стартуют в свободном режиме", (status) => {
    expect(defaultRunModeForStatus(status)).toBe("free");
  });
});
