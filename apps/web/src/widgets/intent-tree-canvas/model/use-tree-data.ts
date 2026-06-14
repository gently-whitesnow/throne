import { useMemo } from "react";

import {
  compareSortKeys,
  contextToParams,
  useIntents,
  useLinksSummary,
  type IntentListItem
} from "@/entities/intent";
import { errorMessage } from "@/shared/lib";

import { layoutTree } from "./layout";
import { parentsFromSummary } from "./parents";
import type { TreeLoadState, TreeNode } from "./tree-data";
import { useResolvedExpansion } from "./use-resolved-expansion";

export function useTreeData(
  context: string | null,
  showResolved: boolean
): TreeLoadState {
  // Контекст целиком выражается серверными фильтрами, поэтому канвас тянет
  // только нужный бакет (а не весь список) через тот же list-эндпоинт, что и
  // доска. useIntents — facade поверх курсорной пагинации, добирает страницы
  // отфильтрованного контекста.
  const params = useMemo(() => contextToParams(context), [context]);
  const intentsQuery = useIntents(params);
  const items = intentsQuery.data ?? null;

  const error = intentsQuery.isError
    ? errorMessage(intentsQuery.error, { base: "Не удалось загрузить intents" })
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
  // Resolved (done) neighbours, transitively expanded from the visible set.
  // Disabled (zero fetches) while the toggle is off.
  const expansion = useResolvedExpansion(showResolved, visibleIds);

  return useMemo<TreeLoadState>(() => {
    if (error !== null) return { kind: "error", message: error };
    if (items === null) return { kind: "loading" };

    const activeIdSet = new Set(visibleIds);

    // Resolved (done) neighbours pulled in by the toggle, sorted for a stable
    // layout. Drop any that somehow collide with a live id.
    const resolvedNodes = showResolved
      ? [...expansion.items.values()]
          .filter((d) => !activeIdSet.has(d.id))
          .sort((a, b) => compareSortKeys(a.sort_key, b.sort_key))
      : [];

    // Combined visible set lets `parentsFromSummary` keep edges that cross
    // between live and resolved nodes (both directions) instead of dropping
    // them as it does for genuinely off-canvas peers.
    const combinedVisible = new Set<string>(activeIdSet);
    for (const node of resolvedNodes) combinedVisible.add(node.id);

    const nodes: TreeNode[] = visible.map((intent) => ({
      id: intent.id,
      intent,
      resolved: false,
      parents: parentsFromSummary(
        linksSummary.get(intent.id),
        combinedVisible,
        intent.id
      )
    }));
    for (const intent of resolvedNodes) {
      nodes.push({
        id: intent.id,
        intent,
        resolved: true,
        parents: parentsFromSummary(
          expansion.summaries.get(intent.id),
          combinedVisible,
          intent.id
        )
      });
    }

    const { pos, bounds } = layoutTree(nodes);
    const byId = new Map<string, TreeNode>();
    for (const node of nodes) byId.set(node.id, node);
    return {
      kind: "ready",
      model: { nodes, byId, positions: pos, bounds }
    };
  }, [
    error,
    items,
    linksSummary,
    visible,
    visibleIds,
    showResolved,
    expansion
  ]);
}
