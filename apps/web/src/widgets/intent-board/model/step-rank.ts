import type { LinksSummaryMap } from "./useLinksSummary";

/**
 * Transitive depth of an intent in the local `blocks`-DAG. Intents with no
 * incoming `blocks` edges (or no edges from currently-visible peers) get
 * rank 1 — «do this now». An intent gets `rank = 1 + max(rank of blocking
 * peer)`, computed only over peers that are also in the visible set so the
 * answer is stable per-page.
 *
 * Cycles are tolerated: when traversal re-enters a node, the cycle edge is
 * treated as «no constraint» so the function still terminates with finite
 * ranks. The board allows blocks-cycles (postanovka родительского intent),
 * so this isn't an error path — just a graceful fallback.
 */
export function computeStepRanks(
  visibleIds: readonly string[],
  summary: LinksSummaryMap
): ReadonlyMap<string, number> {
  const visible = new Set(visibleIds);
  const ranks = new Map<string, number>();
  const stack = new Set<string>();

  function visit(id: string): number {
    const cached = ranks.get(id);
    if (cached !== undefined) return cached;
    if (stack.has(id)) return 1;
    stack.add(id);

    let max = 0;
    const entry = summary.get(id);
    if (entry) {
      for (const peer of entry.blocked_by) {
        if (!visible.has(peer.id)) continue;
        const peerRank = visit(peer.id);
        if (peerRank > max) max = peerRank;
      }
    }

    stack.delete(id);
    const rank = 1 + max;
    ranks.set(id, rank);
    return rank;
  }

  for (const id of visibleIds) {
    visit(id);
  }
  return ranks;
}
