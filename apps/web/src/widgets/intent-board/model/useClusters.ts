import { useCallback, useEffect, useMemo, useState } from "react";

import type { LinksSummaryMap } from "./useLinksSummary";

export interface ClusterInfo {
  /** Stable cluster id — `min(intent_id)` over members. */
  clusterId: string;
  /** Members in display order: blocks-then-derived topo-sort, ties by id. */
  memberIds: readonly string[];
  /** Intersection of tag names across all members; empty when none. */
  commonTags: readonly string[];
}

export interface ClustersResult {
  /** intentId → cluster id (only set for members of size-≥2 clusters). */
  byIntent: ReadonlyMap<string, string>;
  /** cluster id → cluster info (only size-≥2 clusters). */
  clusters: ReadonlyMap<string, ClusterInfo>;
}

interface ClusterInput {
  id: string;
  tagNames: readonly string[];
}

/**
 * Group visible intents into connected components by any link type
 * (`blocks`, `derived_from` / `source_of`, `relates`) using union-find.
 * Singletons are intentionally excluded from the result — the host treats
 * any id absent from `byIntent` as a top-level singleton row.
 *
 * Cluster id = lexicographically smallest member id. Stable across renders
 * for the same visible set, so localStorage keys (collapsed state) survive
 * incidental reorderings.
 */
export function computeClusters(
  items: readonly ClusterInput[],
  summary: LinksSummaryMap
): ClustersResult {
  if (items.length === 0) return { byIntent: new Map(), clusters: new Map() };

  const parent = new Map<string, string>();
  const find = (x: string): string => {
    let cur = parent.get(x) ?? x;
    while (cur !== (parent.get(cur) ?? cur)) cur = parent.get(cur) ?? cur;
    // Path compression.
    let walk = x;
    while ((parent.get(walk) ?? walk) !== cur) {
      const next = parent.get(walk) ?? walk;
      parent.set(walk, cur);
      walk = next;
    }
    return cur;
  };
  const union = (a: string, b: string) => {
    const ra = find(a);
    const rb = find(b);
    if (ra === rb) return;
    // Smaller id wins → cluster id = min(member ids).
    if (ra < rb) parent.set(rb, ra);
    else parent.set(ra, rb);
  };

  const visible = new Set<string>();
  for (const it of items) {
    parent.set(it.id, it.id);
    visible.add(it.id);
  }

  for (const it of items) {
    const entry = summary.get(it.id);
    if (!entry) continue;
    for (const peer of entry.blocked_by)
      if (visible.has(peer.id)) union(it.id, peer.id);
    for (const peer of entry.derived_from)
      if (visible.has(peer.id)) union(it.id, peer.id);
    for (const peer of entry.source_of)
      if (visible.has(peer.id)) union(it.id, peer.id);
    for (const peer of entry.relates)
      if (visible.has(peer.id)) union(it.id, peer.id);
  }

  const grouped = new Map<string, string[]>();
  for (const it of items) {
    const root = find(it.id);
    const arr = grouped.get(root);
    if (arr) arr.push(it.id);
    else grouped.set(root, [it.id]);
  }

  const tagsById = new Map<string, readonly string[]>();
  for (const it of items) tagsById.set(it.id, it.tagNames);

  const byIntent = new Map<string, string>();
  const clusters = new Map<string, ClusterInfo>();
  for (const [root, members] of grouped) {
    if (members.length < 2) continue;
    const ordered = topoOrder(members, summary);
    for (const m of ordered) byIntent.set(m, root);
    clusters.set(root, {
      clusterId: root,
      memberIds: ordered,
      commonTags: intersectTags(ordered, tagsById)
    });
  }
  return { byIntent, clusters };
}

/**
 * Kahn-style topo sort over (`blocks`, `derived_from`) precedence edges. When
 * a cycle is reached the remaining nodes are flushed in id order so the
 * function always terminates. Ties broken by id for determinism.
 */
function topoOrder(memberIds: string[], summary: LinksSummaryMap): string[] {
  const memberSet = new Set(memberIds);
  const remaining = new Map<string, Set<string>>();
  for (const id of memberIds) remaining.set(id, new Set());
  for (const id of memberIds) {
    const entry = summary.get(id);
    if (!entry) continue;
    const preds = remaining.get(id);
    if (!preds) continue;
    for (const peer of entry.blocked_by)
      if (memberSet.has(peer.id) && peer.id !== id) preds.add(peer.id);
    for (const peer of entry.derived_from)
      if (memberSet.has(peer.id) && peer.id !== id) preds.add(peer.id);
  }
  const emitted = new Set<string>();
  const result: string[] = [];
  while (result.length < memberIds.length) {
    const ready = memberIds.filter(
      (id) => !emitted.has(id) && (remaining.get(id)?.size ?? 0) === 0
    );
    if (ready.length === 0) {
      for (const id of memberIds)
        if (!emitted.has(id)) {
          emitted.add(id);
          result.push(id);
        }
      break;
    }
    ready.sort();
    for (const id of ready) {
      emitted.add(id);
      result.push(id);
      for (const preds of remaining.values()) preds.delete(id);
    }
  }
  return result;
}

function intersectTags(
  memberIds: readonly string[],
  tagsById: ReadonlyMap<string, readonly string[]>
): readonly string[] {
  if (memberIds.length === 0) return [];
  const first = tagsById.get(memberIds[0] ?? "") ?? [];
  if (first.length === 0) return [];
  let acc = new Set(first);
  for (let i = 1; i < memberIds.length; i += 1) {
    const next = new Set(tagsById.get(memberIds[i] ?? "") ?? []);
    const intersect = new Set<string>();
    for (const t of acc) if (next.has(t)) intersect.add(t);
    acc = intersect;
    if (acc.size === 0) return [];
  }
  return [...acc].sort();
}

const STORAGE_PREFIX = "throne.cluster.collapsed.";

/**
 * Per-cluster collapsed flag, persisted in localStorage so the choice
 * survives reloads. Default is expanded.
 */
export function useClusterCollapsedState(): {
  isCollapsed: (clusterId: string) => boolean;
  toggle: (clusterId: string) => void;
} {
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(
    () => new Set()
  );

  useEffect(() => {
    if (typeof localStorage === "undefined") return;
    const next = new Set<string>();
    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i);
      if (!key?.startsWith(STORAGE_PREFIX)) continue;
      if (localStorage.getItem(key) === "1") {
        next.add(key.slice(STORAGE_PREFIX.length));
      }
    }
    setCollapsed(next);
  }, []);

  const toggle = useCallback((clusterId: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      const key = STORAGE_PREFIX + clusterId;
      if (next.has(clusterId)) {
        next.delete(clusterId);
        if (typeof localStorage !== "undefined") localStorage.removeItem(key);
      } else {
        next.add(clusterId);
        if (typeof localStorage !== "undefined") localStorage.setItem(key, "1");
      }
      return next;
    });
  }, []);

  const isCollapsed = useCallback(
    (clusterId: string) => collapsed.has(clusterId),
    [collapsed]
  );

  return useMemo(() => ({ isCollapsed, toggle }), [isCollapsed, toggle]);
}
