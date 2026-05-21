import { useCallback, useEffect, useMemo, useState } from "react";

import {
  compareSortKeys,
  matchesContext,
  useLinksSummary,
  type IntentListItem
} from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";

import { layoutTree } from "./layout";
import { parentsFromSummary } from "./parents";
import type { TreeLoadState, TreeNode } from "./tree-data";

export function useTreeData(context: string | null): TreeLoadState {
  const [items, setItems] = useState<IntentListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setError(null);
    httpGet<IntentListItem[]>(intentsEndpoints.listIntents(), controller.signal)
      .then((data) => {
        setItems(data);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setError(
          err instanceof HttpError
            ? `Не удалось загрузить intents (${String(err.status)}).`
            : "Не удалось загрузить intents."
        );
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  useRealtimeEvent("intent.created", reload);
  useRealtimeEvent("intent.deleted", reload);
  useRealtimeEvent("intent.text_changed", reload);
  useRealtimeEvent("intent.status_changed", reload);
  useRealtimeEvent("intent.tags_changed", reload);
  // Pin events change badges on the cards even though they don't move the DAG.
  useRealtimeEvent("intent.pinned", reload);
  useRealtimeEvent("intent.unpinned", reload);
  useRealtimeEvent("intent.pin_moved", reload);

  // Stable ordering — same byte-wise sort_key compare the board uses.
  const ordered = useMemo<IntentListItem[]>(() => {
    if (!items) return [];
    return [...items].sort((a, b) => compareSortKeys(a.sort_key, b.sort_key));
  }, [items]);

  const visible = useMemo<IntentListItem[]>(
    () => ordered.filter((i) => matchesContext(i, context)),
    [context, ordered]
  );

  const visibleIds = useMemo(() => visible.map((i) => i.id), [visible]);
  const linksSummary = useLinksSummary(visibleIds);

  return useMemo<TreeLoadState>(() => {
    if (error !== null) return { kind: "error", message: error };
    if (items === null) return { kind: "loading" };

    const visibleSet = new Set(visibleIds);
    const nodes: TreeNode[] = visible.map((intent) => ({
      id: intent.id,
      intent,
      parents: parentsFromSummary(
        linksSummary.get(intent.id),
        visibleSet,
        intent.id
      )
    }));
    const { pos, bounds } = layoutTree(nodes);
    const byId = new Map<string, TreeNode>();
    for (const node of nodes) byId.set(node.id, node);
    return {
      kind: "ready",
      model: { nodes, byId, positions: pos, bounds }
    };
  }, [error, items, linksSummary, visible, visibleIds]);
}
