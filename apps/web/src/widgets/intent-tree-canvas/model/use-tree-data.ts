import { useCallback, useEffect, useMemo, useState } from "react";

import {
  compareSortKeys,
  matchesContext,
  useLinksSummary,
  type IntentListItem
} from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { useRealtimeEvent, type RealtimeEventMap } from "@/shared/realtime";

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

  // Patch одной карточки в items, чтобы не дёргать полный refetch+layout —
  // иначе viewport канваса (zoom/pan) сбрасывался бы при каждой смене статуса.
  const patchItem = useCallback(
    (id: string, change: (item: IntentListItem) => IntentListItem) => {
      setItems((prev) =>
        prev === null
          ? prev
          : prev.map((it) => (it.id === id ? change(it) : it))
      );
    },
    []
  );

  const onStatusChanged = useCallback<
    (payload: RealtimeEventMap["intent.status_changed"]) => void
  >(
    (payload) => {
      patchItem(payload.id, (it) => ({
        ...it,
        status: payload.status,
        current_version: payload.current_version,
        updated_at: payload.updated_at
      }));
    },
    [patchItem]
  );

  const onTagsChanged = useCallback<
    (payload: RealtimeEventMap["intent.tags_changed"]) => void
  >(
    (payload) => {
      patchItem(payload.id, (it) => ({
        ...it,
        tags: payload.tags,
        current_version: payload.current_version,
        updated_at: payload.updated_at
      }));
    },
    [patchItem]
  );

  const onPinUpserted = useCallback<
    (payload: RealtimeEventMap["intent.pinned"]) => void
  >(
    (payload) => {
      patchItem(payload.intent_id, (it) => ({
        ...it,
        pinned_in: upsertPin(
          it.pinned_in,
          payload.context_tag_id,
          payload.pin_sort_key
        )
      }));
    },
    [patchItem]
  );

  const onUnpinned = useCallback<
    (payload: RealtimeEventMap["intent.unpinned"]) => void
  >(
    (payload) => {
      patchItem(payload.intent_id, (it) => ({
        ...it,
        pinned_in: it.pinned_in.filter(
          (p) => p.context_tag_id !== payload.context_tag_id
        )
      }));
    },
    [patchItem]
  );

  useRealtimeEvent("intent.created", reload);
  useRealtimeEvent("intent.deleted", reload);
  useRealtimeEvent("intent.text_changed", reload);
  useRealtimeEvent("intent.status_changed", onStatusChanged);
  useRealtimeEvent("intent.tags_changed", onTagsChanged);
  useRealtimeEvent("intent.pinned", onPinUpserted);
  useRealtimeEvent("intent.pin_moved", onPinUpserted);
  useRealtimeEvent("intent.unpinned", onUnpinned);

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

function upsertPin(
  pins: IntentListItem["pinned_in"],
  contextTagId: string,
  pinSortKey: string
): IntentListItem["pinned_in"] {
  const idx = pins.findIndex((p) => p.context_tag_id === contextTagId);
  const entry = { context_tag_id: contextTagId, pin_sort_key: pinSortKey };
  if (idx < 0) return [...pins, entry];
  const next = pins.slice();
  next[idx] = entry;
  return next;
}
