import type { EntityListReorder } from "@/shared/ui";

import type { ClusterInfo, ClustersResult } from "./useClusters";

export interface BoardEntry {
  kind: "single" | "cluster";
  /** For 'single': the intent id; for 'cluster': cluster id. */
  anchorId: string;
}

export interface ClusterMove {
  clusterId: string;
  memberIds: readonly string[];
  beforeId: string | null;
  afterId: string | null;
}

/**
 * Build the visible top-level entries from the sort_key-ordered list and the
 * computed cluster map. A cluster is emitted at the position of its first
 * member encountered in `orderedIds` — its other members are pulled out of
 * the flat row sequence and the cluster represents them collectively.
 */
export function buildEntries(
  orderedIds: readonly string[],
  byIntent: ReadonlyMap<string, string>
): BoardEntry[] {
  const emitted = new Set<string>();
  const entries: BoardEntry[] = [];
  for (const id of orderedIds) {
    const clusterId = byIntent.get(id);
    if (!clusterId) {
      entries.push({ kind: "single", anchorId: id });
      continue;
    }
    if (emitted.has(clusterId)) continue;
    emitted.add(clusterId);
    entries.push({ kind: "cluster", anchorId: clusterId });
  }
  return entries;
}

/**
 * Materialize the flat row sequence — every intent that will appear visually
 * in the list, with cluster members in topo order. Used by card-level DnD to
 * compute the pair of neighbours surrounding the drop position.
 */
export function buildFlatIds(
  entries: readonly BoardEntry[],
  clusters: ClustersResult["clusters"]
): string[] {
  const out: string[] = [];
  for (const entry of entries) {
    if (entry.kind === "single") {
      out.push(entry.anchorId);
      continue;
    }
    const c = clusters.get(entry.anchorId);
    if (c) for (const id of c.memberIds) out.push(id);
  }
  return out;
}

/**
 * Resolve a card-level drop into the (beforeId, afterId) anchor pair expected
 * by the moveIntent endpoint. Picks (idx-1, idx) for «before» and (idx, idx+1)
 * for «after», skipping over the moved id if it sits in either slot — so the
 * resulting pair represents the *post-move* neighbours.
 */
export function resolveCardMove(
  movedId: string,
  overId: string,
  position: "before" | "after",
  flatIds: readonly string[]
): EntityListReorder {
  const targetIndex = flatIds.indexOf(overId);
  let beforeIdx = position === "before" ? targetIndex - 1 : targetIndex;
  let afterIdx = position === "before" ? targetIndex : targetIndex + 1;
  if (flatIds[beforeIdx] === movedId) beforeIdx -= 1;
  if (flatIds[afterIdx] === movedId) afterIdx += 1;
  return {
    movedId,
    beforeId: flatIds[beforeIdx] ?? null,
    afterId: flatIds[afterIdx] ?? null
  };
}

/**
 * Resolve a cluster drop into the (beforeId, afterId) pair of intent-level
 * anchors. The neighbours are the last/first intent of the surrounding
 * top-level entries. The host then sequences moveIntent calls for every
 * member of the moved cluster.
 */
export function resolveClusterMove(
  moved: ClusterInfo,
  targetAnchor: string,
  position: "before" | "after",
  entries: readonly BoardEntry[],
  clusters: ClustersResult["clusters"]
): ClusterMove | null {
  const targetIdx = entries.findIndex((e) => e.anchorId === targetAnchor);
  if (targetIdx < 0) return null;
  const movedIdx = entries.findIndex((e) => e.anchorId === moved.clusterId);
  if (movedIdx < 0) return null;
  let slot = position === "before" ? targetIdx : targetIdx + 1;
  if (slot === movedIdx || slot === movedIdx + 1) return null;
  if (slot > movedIdx) slot -= 1;
  const remaining = entries.filter((e) => e.anchorId !== moved.clusterId);
  const beforeId =
    slot - 1 >= 0 ? lastIntentOfEntry(remaining[slot - 1], clusters) : null;
  const afterId =
    slot < remaining.length
      ? firstIntentOfEntry(remaining[slot], clusters)
      : null;
  return {
    clusterId: moved.clusterId,
    memberIds: moved.memberIds,
    beforeId,
    afterId
  };
}

function lastIntentOfEntry(
  entry: BoardEntry,
  clusters: ClustersResult["clusters"]
): string {
  if (entry.kind === "single") return entry.anchorId;
  const c = clusters.get(entry.anchorId);
  if (!c || c.memberIds.length === 0) return entry.anchorId;
  return c.memberIds[c.memberIds.length - 1];
}

function firstIntentOfEntry(
  entry: BoardEntry,
  clusters: ClustersResult["clusters"]
): string {
  if (entry.kind === "single") return entry.anchorId;
  const c = clusters.get(entry.anchorId);
  if (!c || c.memberIds.length === 0) return entry.anchorId;
  return c.memberIds[0];
}
