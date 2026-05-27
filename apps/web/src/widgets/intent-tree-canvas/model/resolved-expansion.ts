import type {
  IntentLinkPeer,
  IntentLinksSummaryEntry
} from "@/entities/intent";

import type { CanvasCardIntent } from "./tree-data";

/** Resolved (done) neighbours pulled onto the canvas by the «show resolved» toggle. */
export interface ResolvedExpansion {
  /** Done node card data, keyed by intent id. */
  items: ReadonlyMap<string, CanvasCardIntent>;
  /** Link summaries for the done nodes, keyed by intent id — used to wire their edges. */
  summaries: ReadonlyMap<string, IntentLinksSummaryEntry>;
}

export const EMPTY_EXPANSION: ResolvedExpansion = {
  items: new Map(),
  summaries: new Map()
};

// Safety rails: a pathologically large «done» component must not lock up the
// canvas. The toggle is an overview aid, not an exhaustive audit.
const MAX_NODES = 800;
const MAX_ROUNDS = 16;
const CHUNK = 100;

type FetchSummaries = (
  ids: readonly string[],
  signal?: AbortSignal
) => Promise<IntentLinksSummaryEntry[]>;

/**
 * Structural neighbours of an entry, in every direction the summary exposes.
 * Excludes `relates` (thematic links) per the canvas contract. Note: `blocks`
 * is only reachable through the blocked end's `blocked_by`, so blocker chains
 * expand upstream while a done node's downstream-blocked peers aren't
 * auto-discovered — acceptable, since done items are normally upstream
 * prerequisites of the active roadmap.
 */
function structuralPeers(entry: IntentLinksSummaryEntry): IntentLinkPeer[] {
  return [...entry.derived_from, ...entry.source_of, ...entry.blocked_by];
}

function peerToCard(peer: IntentLinkPeer): CanvasCardIntent {
  return {
    id: peer.id,
    status: peer.status,
    current_version: peer.current_version,
    tags: peer.tags,
    text_short: peer.text_short,
    sort_key: peer.sort_key,
    // Peers don't carry pin state; resolved ghosts never show the pin marker.
    pinned_in: []
  };
}

async function fetchChunked(
  fetchSummaries: FetchSummaries,
  ids: readonly string[],
  signal?: AbortSignal
): Promise<IntentLinksSummaryEntry[]> {
  const out: IntentLinksSummaryEntry[] = [];
  for (let i = 0; i < ids.length; i += CHUNK) {
    const part = await fetchSummaries(ids.slice(i, i + CHUNK), signal);
    out.push(...part);
  }
  return out;
}

/**
 * BFS the structural link graph from the active (visible) intents and pull in
 * every reachable `done` intent, transitively through other done intents.
 * Active peers are ignored — they're already on the canvas. Returns the done
 * nodes' card data plus their link summaries, so the caller can wire edges with
 * the same `parentsFromSummary` it uses for live nodes.
 */
export async function expandResolved(
  activeIds: readonly string[],
  fetchSummaries: FetchSummaries,
  signal?: AbortSignal
): Promise<ResolvedExpansion> {
  const items = new Map<string, CanvasCardIntent>();
  const summaries = new Map<string, IntentLinksSummaryEntry>();
  if (activeIds.length === 0) return { items, summaries };

  let frontier: string[] = [];
  const consider = (peer: IntentLinkPeer): void => {
    if (peer.status !== "done") return;
    if (items.has(peer.id)) return;
    if (items.size >= MAX_NODES) return;
    items.set(peer.id, peerToCard(peer));
    frontier.push(peer.id);
  };

  // Round 0: active nodes' summaries reveal the first hop of done neighbours.
  const seed = await fetchChunked(fetchSummaries, activeIds, signal);
  for (const entry of seed) {
    for (const peer of structuralPeers(entry)) consider(peer);
  }

  // Subsequent rounds: each discovered done node's summary reveals further done
  // neighbours until the component is exhausted (or a safety rail trips). The
  // `items` dedupe makes cycles terminate.
  let rounds = 0;
  while (frontier.length > 0 && rounds < MAX_ROUNDS && items.size < MAX_NODES) {
    rounds += 1;
    const batch = frontier;
    frontier = [];
    const entries = await fetchChunked(fetchSummaries, batch, signal);
    for (const entry of entries) {
      summaries.set(entry.intent_id, entry);
      for (const peer of structuralPeers(entry)) consider(peer);
    }
  }

  return { items, summaries };
}
