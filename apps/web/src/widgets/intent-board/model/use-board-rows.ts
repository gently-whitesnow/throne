import { useMemo } from "react";
import { useSearchParams } from "react-router-dom";

import {
  intentStatusMeta,
  type IntentListItem,
  type LinksSummaryMap
} from "@/entities/intent";
import type { EntityListRow } from "@/shared/ui";

import { firstLine } from "./board-helpers";

interface BuildRowsArgs {
  visibleItems: readonly IntentListItem[];
  linksSummary: LinksSummaryMap;
  stepRanks: ReadonlyMap<string, number>;
  familyTints: ReadonlyMap<string, string>;
  hoverPeerIds: ReadonlySet<string>;
  runningTerminalIds: ReadonlySet<string>;
}

/**
 * Маппит `IntentListItem` в плоский `EntityListRow`. Вынесено из IntentBoard,
 * чтобы виджет оставался <300 строк и можно было отдельно тестировать сборку
 * строк.
 */
export function useBoardRows({
  visibleItems,
  linksSummary,
  stepRanks,
  familyTints,
  hoverPeerIds,
  runningTerminalIds
}: BuildRowsArgs): EntityListRow[] {
  const [params] = useSearchParams();
  return useMemo<EntityListRow[]>(() => {
    const search = new URLSearchParams(params).toString();
    return visibleItems.map((i) => {
      const status = intentStatusMeta[i.status];
      const tagNames = i.tags.map((t) => t.name);
      const href =
        search.length > 0 ? `/intents/${i.id}?${search}` : `/intents/${i.id}`;
      const summary = linksSummary.get(i.id);
      const blockedCount = summary?.blocked_by.length ?? 0;
      const step = stepRanks.get(i.id) ?? 1;
      const isPeer = hoverPeerIds.has(i.id);
      return {
        id: i.id,
        title: firstLine(i.text_short) || i.id,
        subtitle: tagNames.length > 0 ? `#${tagNames.join(" #")}` : undefined,
        meta: `v${String(i.current_version)}`,
        badge: status.label,
        badgeColor: status.surface,
        badgeTextColor: status.ink,
        href,
        // Step rank surfaces only when something blocks this card. Step 1 is
        // «do it now» — implicit, no chip needed.
        warning: step > 1 ? `Шаг ${String(step)}` : undefined,
        warningTitle:
          step > 1
            ? `Можно начать после ${String(step - 1)} ${pluralizeSteps(step - 1)} зависимостей (всего блокирующих здесь: ${String(blockedCount)}).`
            : undefined,
        outline: isPeer
          ? "outline outline-2 outline-primary/40 outline-offset-[-2px] z-10"
          : undefined,
        tint: familyTints.get(i.id),
        pinned: i.pinned_in.length > 0,
        live: runningTerminalIds.has(i.id)
      };
    });
  }, [
    familyTints,
    hoverPeerIds,
    linksSummary,
    params,
    runningTerminalIds,
    stepRanks,
    visibleItems
  ]);
}

function pluralizeSteps(n: number): string {
  // Russian pluralisation: 1 — «шага», 2..4 — «шагов», 5+ — «шагов». Keep it
  // simple: «шага» for 1, «шагов» for the rest. This text only appears in a
  // tooltip so a perfect form isn't worth a library dependency.
  return n === 1 ? "шага" : "шагов";
}
