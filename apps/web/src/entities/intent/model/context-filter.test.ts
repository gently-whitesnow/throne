import { describe, expect, it } from "vitest";

import { boardContext } from "@/shared/lib";

import { contextToParams, matchesContext } from "./context-filter";
import type { IntentListItem } from "./types";

describe("contextToParams for board contexts", () => {
  it("maps a board context to an active-status board filter", () => {
    const params = contextToParams(boardContext("kaiten", "board-7"));

    expect(params.tracker).toBe("kaiten");
    expect(params.board).toBe("board-7");
    expect(params.status).toEqual([
      "draft",
      "interview",
      "ready_for_work",
      "work",
      "awaiting_operator"
    ]);
    expect(params.untagged).toBeUndefined();
    expect(params.tag).toBeUndefined();
  });

  it("extracts the board id even when it contains digits and dashes", () => {
    const params = contextToParams(boardContext("kaiten", "99-2"));

    expect(params.tracker).toBe("kaiten");
    expect(params.board).toBe("99-2");
  });
});

describe("matchesContext for board contexts", () => {
  it("never matches client-side — board membership is server-fetched only", () => {
    const item = {
      status: "draft",
      tags: [],
      pinned_in: []
    } as unknown as IntentListItem;

    expect(matchesContext(item, boardContext("kaiten", "board-7"))).toBe(false);
  });
});
