import { describe, expect, it } from "vitest";

import { boardContext, PINNED_CONTEXT, UNTAGGED_CONTEXT } from "@/shared/lib";

import {
  contextToParams,
  contextTitle,
  matchesContext
} from "./context-filter";
import type { IntentListItem } from "./types";

const ACTIVE_STATUSES = [
  "draft",
  "interview",
  "ready_for_work",
  "work",
  "awaiting_operator"
];

// Board is no longer an intent facet — a board context becomes a card browser
// in the middle pane and never reaches these helpers. The helpers therefore
// treat it like an unknown context (i.e. a tag name), which is inert here.
describe("contextToParams for non-board contexts", () => {
  it("maps a tag context to an active-status tag filter", () => {
    const params = contextToParams("frontend");

    expect(params.status).toEqual(ACTIVE_STATUSES);
    expect(params.tag).toBe("frontend");
    expect(params.tracker).toBeUndefined();
    expect(params.board).toBeUndefined();
  });

  it("maps the untagged context to an untagged active filter", () => {
    const params = contextToParams(UNTAGGED_CONTEXT);

    expect(params.status).toEqual(ACTIVE_STATUSES);
    expect(params.untagged).toBe(true);
  });

  it("maps the pinned context to a pinned filter", () => {
    expect(contextToParams(PINNED_CONTEXT)).toEqual({ pinned: true });
  });

  it("no longer emits tracker/board params for a board context", () => {
    const params = contextToParams(boardContext("kaiten", "board-7"));

    expect(params.tracker).toBeUndefined();
    expect(params.board).toBeUndefined();
  });
});

describe("matchesContext for tag contexts", () => {
  it("matches an active intent carrying the tag", () => {
    const item = {
      status: "draft",
      tags: [{ name: "frontend" }],
      pinned_in: []
    } as unknown as IntentListItem;

    expect(matchesContext(item, "frontend")).toBe(true);
  });

  it("does not match an intent without the tag", () => {
    const item = {
      status: "draft",
      tags: [],
      pinned_in: []
    } as unknown as IntentListItem;

    expect(matchesContext(item, "frontend")).toBe(false);
  });
});

describe("contextTitle", () => {
  it("labels a tag context with a hash prefix", () => {
    expect(contextTitle("frontend")).toBe("# frontend");
  });
});
