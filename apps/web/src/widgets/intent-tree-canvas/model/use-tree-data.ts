import { useMemo } from "react";

import {
  compareSortKeys,
  contextToParams,
  useIntents,
  useLinksSummary,
  type IntentListItem
} from "@/entities/intent";
import { HttpError } from "@/shared/api";

import { layoutTree } from "./layout";
import { parentsFromSummary } from "./parents";
import type { TreeLoadState, TreeNode } from "./tree-data";

export function useTreeData(context: string | null): TreeLoadState {
  // Контекст целиком выражается серверными фильтрами, поэтому канвас тянет
  // только нужный бакет (а не весь список) через тот же list-эндпоинт, что и
  // доска. useIntents — facade поверх курсорной пагинации, добирает страницы
  // отфильтрованного контекста.
  const params = useMemo(() => contextToParams(context), [context]);
  const intentsQuery = useIntents(params);
  const items = intentsQuery.data ?? null;

  const error = intentsQuery.isError
    ? intentsQuery.error instanceof HttpError
      ? `Не удалось загрузить intents (${String(intentsQuery.error.status)}).`
      : "Не удалось загрузить intents."
    : null;

  // Realtime-апдейты (status/tags/pin/reordered/text_changed) идут через
  // app/realtime-query-bridge.tsx — он точечно патчит элементы внутри кеша
  // intents/list, чтобы viewport канваса (zoom/pan) не сбрасывался при каждой
  // смене статуса. List-shape события (created/deleted) инвалидируют список
  // целиком и приводят к полному рефетчу.

  // Stable ordering — same byte-wise sort_key compare the board uses. Items are
  // already scoped to the context server-side, so this is the full visible set.
  const visible = useMemo<IntentListItem[]>(() => {
    if (!items) return [];
    return [...items].sort((a, b) => compareSortKeys(a.sort_key, b.sort_key));
  }, [items]);

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
