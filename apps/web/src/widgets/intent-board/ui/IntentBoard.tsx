import { useQueryClient } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useCallback, useMemo, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import {
  compareSortKeys,
  intentsQueryKeys,
  intentStatusMeta,
  useIntents,
  useLinksSummary,
  type IntentListItem
} from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { moveIntent } from "@/features/move-intent";
import { HttpError } from "@/shared/api";
import { isTagContext } from "@/shared/lib";
import {
  EntityList,
  type EntityListReorder,
  type EntityListRow
} from "@/shared/ui";

import {
  contextTitle,
  emptyMessage,
  firstLine,
  matchesContext,
  synthesizeSortKey
} from "../model/board-helpers";
import { computeFamilyTints } from "../model/family-tint";
import { computeStepRanks } from "../model/step-rank";
import { IntentLinksOverlay } from "./IntentLinksOverlay";
import { PinnedSection } from "./PinnedSection";

const LINKS_RAIL_WIDTH = 36;
const EMPTY_ITEMS: readonly IntentListItem[] = [];

interface IntentBoardProps {
  headerAction?: ReactNode;
}

export function IntentBoard({ headerAction }: IntentBoardProps = {}) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const queryClient = useQueryClient();
  const intentsQuery = useIntents();

  const context = params.get("context");

  const allItems: readonly IntentListItem[] = intentsQuery.data ?? EMPTY_ITEMS;
  const errorMessage = intentsQuery.isError
    ? intentsQuery.error instanceof HttpError
      ? `Не удалось загрузить intents (${String(intentsQuery.error.status)}).`
      : "Не удалось загрузить intents."
    : null;

  // Realtime-инвалидация всех ключей intents выполняется централизованно в
  // app/realtime-query-bridge.tsx. Locally — только optimistic update в
  // handleReorder; авторитетный sort_key прилетает событием
  // `intent.reordered` и заменяет placeholder.

  // Server-defined order is sort_key ASC, ordinal. Use compareSortKeys (byte-wise)
  // — never localeCompare, see entities/intent/model/sortKey.ts for why.
  const orderedItems = useMemo(
    () => [...allItems].sort((a, b) => compareSortKeys(a.sort_key, b.sort_key)),
    [allItems]
  );

  const visibleItems = useMemo(
    () => orderedItems.filter((i) => matchesContext(i, context)),
    [context, orderedItems]
  );
  const tagNameToId = useMemo(() => {
    const map = new Map<string, string>();
    for (const item of allItems) {
      for (const tag of item.tags) map.set(tag.name, tag.id);
    }
    return map;
  }, [allItems]);
  const currentContextTagId =
    context && isTagContext(context)
      ? (tagNameToId.get(context) ?? null)
      : null;
  const pinnedIntents = useMemo(
    () =>
      currentContextTagId === null
        ? []
        : allItems.filter((i) =>
            i.pinned_in.some((p) => p.context_tag_id === currentContextTagId)
          ),
    [allItems, currentContextTagId]
  );

  const visibleIds = useMemo(
    () => visibleItems.map((i) => i.id),
    [visibleItems]
  );
  const linksSummary = useLinksSummary(visibleIds);
  const hasAnyLinks = useMemo(() => {
    // Рельса рисует ребро только если оба конца — видимые строки. Связь к
    // невидимому интенту overlay пропускает, поэтому она не должна резервировать
    // пустое место справа.
    for (const [ownerId, entry] of linksSummary) {
      for (const peer of entry.blocked_by) {
        if (peer.id !== ownerId && linksSummary.has(peer.id)) return true;
      }
      for (const peer of entry.derived_from) {
        if (peer.id !== ownerId && linksSummary.has(peer.id)) return true;
      }
      for (const peer of entry.relates) {
        if (peer.id !== ownerId && linksSummary.has(peer.id)) return true;
      }
    }
    return false;
  }, [linksSummary]);
  // Bumps whenever the visible-id sequence changes; drives the overlay's
  // re-measurement without subscribing to scroll/resize events.
  const layoutSignature = useMemo(() => visibleIds.join("|"), [visibleIds]);
  const stepRanks = useMemo(
    () => computeStepRanks(visibleIds, linksSummary),
    [linksSummary, visibleIds]
  );
  const familyTints = useMemo(
    () => computeFamilyTints(visibleIds, linksSummary),
    [linksSummary, visibleIds]
  );

  const [hoveredId, setHoveredId] = useState<string | null>(null);
  const rowRefs = useRef<Map<string, HTMLLIElement>>(new Map());
  // Wrapper around the list and the SVG overlay. Together they share one
  // positioning context that grows with the list height — so when the scroll
  // container scrolls, SVG and rows translate as a single layer and the
  // arrow geometry stays glued to the cards without any scroll listener.
  const listLayerRef = useRef<HTMLDivElement | null>(null);
  const handleRowRef = useCallback((id: string, el: HTMLLIElement | null) => {
    if (el) {
      rowRefs.current.set(id, el);
    } else {
      rowRefs.current.delete(id);
    }
  }, []);
  const hoverPeerIds = useMemo(() => {
    if (!hoveredId) return new Set<string>();
    const entry = linksSummary.get(hoveredId);
    if (!entry) return new Set<string>();
    return new Set<string>([
      ...entry.blocked_by.map((p) => p.id),
      ...entry.derived_from.map((p) => p.id),
      ...entry.source_of.map((p) => p.id),
      ...entry.relates.map((p) => p.id)
    ]);
  }, [hoveredId, linksSummary]);

  const rows = useMemo<EntityListRow[]>(() => {
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
        pinned: i.pinned_in.length > 0
      };
    });
  }, [
    familyTints,
    hoverPeerIds,
    linksSummary,
    params,
    stepRanks,
    visibleItems
  ]);

  const invalidateList = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: intentsQueryKeys.list() });
  }, [queryClient]);

  const handleReorder = useCallback(
    ({ movedId, beforeId, afterId }: EntityListReorder) => {
      if (!beforeId && !afterId) return;
      // Optimistic: place a synthetic sort_key between the neighbours so the local
      // ordering matches the dropped position immediately. The realtime event with
      // the authoritative key arrives later and replaces this placeholder.
      const placeholder = synthesizeSortKey(allItems, beforeId, afterId);
      queryClient.setQueryData<IntentListItem[]>(
        intentsQueryKeys.list(),
        (prev) =>
          prev?.map((i) =>
            i.id === movedId ? { ...i, sort_key: placeholder } : i
          ) ?? prev
      );

      moveIntent({ intentId: movedId, beforeId, afterId }).catch(() => {
        invalidateList();
      });
    },
    [allItems, invalidateList, queryClient]
  );

  const handleCreated = (intentId: string) => {
    invalidateList();
    const search = params.toString();
    const target =
      search.length > 0
        ? `/intents/${intentId}?${search}`
        : `/intents/${intentId}`;
    void navigate(target);
  };

  return (
    <section
      className="flex min-h-0 min-w-0 flex-col overflow-hidden border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Список Intents"
    >
      <div className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 truncate text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          {contextTitle(context)}
        </h2>
        <div className="flex items-center gap-2">
          {headerAction}
          <CreateIntentButton
            onCreated={(intent) => {
              handleCreated(intent.id);
            }}
          />
        </div>
      </div>
      {intentsQuery.isSuccess && isTagContext(context) ? (
        <PinnedSection
          items={allItems}
          currentContextTagId={currentContextTagId}
          currentContextLabel={context ?? ""}
          pinnedIntents={pinnedIntents}
          onMutationFailed={invalidateList}
        />
      ) : null}
      <div className="min-h-0 flex-1 overflow-y-auto">
        {intentsQuery.isPending && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        )}
        {errorMessage !== null && (
          <p
            role="alert"
            className="m-0 px-3.5 py-4 text-[13px] text-base-content/60"
          >
            {errorMessage}
          </p>
        )}
        {intentsQuery.isSuccess && (
          <div
            ref={listLayerRef}
            className="relative"
            style={hasAnyLinks ? { paddingRight: LINKS_RAIL_WIDTH } : undefined}
          >
            <EntityList
              items={rows}
              emptyMessage={emptyMessage(context, allItems.length)}
              onReorder={handleReorder}
              onRowHover={setHoveredId}
              rowRef={handleRowRef}
            />
            {hasAnyLinks && (
              <IntentLinksOverlay
                hoveredId={hoveredId}
                summary={linksSummary}
                rowRefs={rowRefs.current}
                containerRef={listLayerRef}
                railWidth={LINKS_RAIL_WIDTH}
                layoutSignature={layoutSignature}
              />
            )}
          </div>
        )}
      </div>
    </section>
  );
}

function pluralizeSteps(n: number): string {
  // Russian pluralisation: 1 — «шага», 2..4 — «шагов», 5+ — «шагов». Keep it
  // simple: «шага» for 1, «шагов» for the rest. This text only appears in a
  // tooltip so a perfect form isn't worth a library dependency.
  return n === 1 ? "шага" : "шагов";
}
