import { describe, expect, it, vi } from "vitest";

import type {
  IntentLinkPeer,
  IntentLinksSummaryEntry,
  IntentStatus
} from "@/entities/intent";

import { expandResolved } from "./resolved-expansion";

function peer(id: string, status: IntentStatus): IntentLinkPeer {
  return {
    id,
    status,
    current_version: 1,
    sort_key: id,
    text_short: `text ${id}`,
    tags: []
  };
}

function entry(
  id: string,
  parts: Partial<Omit<IntentLinksSummaryEntry, "intent_id">>
): IntentLinksSummaryEntry {
  return {
    intent_id: id,
    blocked_by: parts.blocked_by ?? [],
    blocks: parts.blocks ?? [],
    linked_from: parts.linked_from ?? [],
    linked_to: parts.linked_to ?? []
  };
}

/** Fake links-summary endpoint backed by an in-memory graph. Mirrors the real
 *  one: returns an entry only for requested ids that have incident edges. */
function fakeFetcher(graph: ReadonlyMap<string, IntentLinksSummaryEntry>) {
  return vi.fn((ids: readonly string[]): Promise<IntentLinksSummaryEntry[]> =>
    Promise.resolve(
      ids.flatMap((id) => {
        const e = graph.get(id);
        return e ? [e] : [];
      })
    )
  );
}

describe("expandResolved", () => {
  it("returns empty result for no active ids", async () => {
    const fetcher = fakeFetcher(new Map());
    const result = await expandResolved([], fetcher);
    expect(result.items.size).toBe(0);
    expect(result.summaries.size).toBe(0);
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("pulls in done neighbours transitively through other done nodes", async () => {
    // D1(done) → A1, D3(done) → D1
    // A2 → blocked_by D2(done)
    const graph = new Map<string, IntentLinksSummaryEntry>([
      [
        "A1",
        entry("A1", {
          linked_from: [peer("D1", "done")]
        })
      ],
      ["A2", entry("A2", { blocked_by: [peer("D2", "done")] })],
      [
        "D1",
        entry("D1", {
          linked_from: [peer("D3", "done")],
          linked_to: [peer("A1", "work")]
        })
      ],
      ["D2", entry("D2", { blocks: [peer("A2", "work")] })],
      ["D3", entry("D3", { linked_to: [peer("D1", "done")] })]
    ]);

    const result = await expandResolved(["A1", "A2"], fakeFetcher(graph));

    expect([...result.items.keys()].sort()).toEqual(["D1", "D2", "D3"]);
    // Active nodes are never added as resolved items.
    expect(result.items.has("A1")).toBe(false);
    expect(result.items.has("A2")).toBe(false);
    // Done nodes' own summaries are captured for edge wiring.
    expect([...result.summaries.keys()].sort()).toEqual(["D1", "D2", "D3"]);
  });

  it("maps peers onto canvas cards with empty pin state", async () => {
    const graph = new Map<string, IntentLinksSummaryEntry>([
      ["A1", entry("A1", { linked_from: [peer("D1", "done")] })],
      ["D1", entry("D1", { linked_to: [peer("A1", "work")] })]
    ]);

    const result = await expandResolved(["A1"], fakeFetcher(graph));
    const card = result.items.get("D1");
    expect(card?.status).toBe("done");
    expect(card?.text_short).toBe("text D1");
    expect(card?.pinned_in).toEqual([]);
  });

  it("terminates on a done↔done cycle without re-fetching", async () => {
    const graph = new Map<string, IntentLinksSummaryEntry>([
      ["A1", entry("A1", { linked_from: [peer("D1", "done")] })],
      ["D1", entry("D1", { linked_from: [peer("D2", "done")] })],
      ["D2", entry("D2", { linked_from: [peer("D1", "done")] })]
    ]);

    const fetcher = fakeFetcher(graph);
    const result = await expandResolved(["A1"], fetcher);

    expect([...result.items.keys()].sort()).toEqual(["D1", "D2"]);
    // Each id is fetched at most once: seed [A1], then [D1, D2]; no re-fetch.
    const fetched = fetcher.mock.calls.flatMap((c) => [...c[0]]);
    expect(fetched.sort()).toEqual(["A1", "D1", "D2"]);
  });
});
