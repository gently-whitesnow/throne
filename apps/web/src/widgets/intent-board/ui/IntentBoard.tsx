import { useQueryClient, type InfiniteData } from "@tanstack/react-query";
import type { VirtualItem } from "@tanstack/react-virtual";
import type { ReactNode } from "react";
import { useCallback, useMemo, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import { intentsQueryKeys, useLinksSummary } from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { moveIntent } from "@/features/move-intent";
import { type IntentsComponents } from "@/shared/api";
import { errorMessage, isTagContext } from "@/shared/lib";
import { VirtualEntityList, type EntityListReorder } from "@/shared/ui";

import {
  contextTitle,
  emptyMessage,
  synthesizeSortKey
} from "../model/board-helpers";
import { computeFamilyTints } from "../model/family-tint";
import { computeStepRanks } from "../model/step-rank";
import { useBoardItems } from "../model/use-board-items";
import { useBoardRows } from "../model/use-board-rows";
import { IntentLinksOverlay } from "./IntentLinksOverlay";
import { PinnedSection } from "./PinnedSection";

type IntentListPage = IntentsComponents["schemas"]["IntentListPageDto"];
type IntentListInfinite = InfiniteData<IntentListPage, string | undefined>;

const LINKS_RAIL_WIDTH = 36;

interface IntentBoardProps {
  headerAction?: ReactNode;
}

export function IntentBoard({ headerAction }: IntentBoardProps = {}) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const queryClient = useQueryClient();
  const context = params.get("context");

  const {
    allItems,
    visibleItems,
    isPending,
    isSuccess,
    isError,
    error,
    hasNextPage,
    isFetchingNextPage,
    fetchNextPage
  } = useBoardItems(context);

  const loadErrorMessage = isError
    ? errorMessage(error, { base: "Не удалось загрузить intents" })
    : null;

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

  // Виртуализатор сообщает viewport — только эти id нужны для рисования
  // связей и подсветки. Без ограничения id'шников запрос links-summary раздувал
  // querystring и упирался в 414.
  const [visibleVirtual, setVisibleVirtual] = useState<readonly VirtualItem[]>(
    []
  );
  const viewportIds = useMemo(() => {
    if (visibleVirtual.length === 0) return [] as string[];
    const ids: string[] = [];
    for (const v of visibleVirtual) {
      // Виртуализатор может на 1 тик отстать от items при смене контекста —
      // index может выйти за длину массива. Защищаемся явной проверкой.
      if (v.index >= visibleItems.length) continue;
      ids.push(visibleItems[v.index].id);
    }
    return ids;
  }, [visibleItems, visibleVirtual]);

  const linksSummary = useLinksSummary(viewportIds);
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
  const layoutSignature = useMemo(() => viewportIds.join("|"), [viewportIds]);
  const stepRanks = useMemo(
    () => computeStepRanks(viewportIds, linksSummary),
    [linksSummary, viewportIds]
  );
  const familyTints = useMemo(
    () => computeFamilyTints(viewportIds, linksSummary),
    [linksSummary, viewportIds]
  );

  const [hoveredId, setHoveredId] = useState<string | null>(null);
  const rowRefs = useRef<Map<string, HTMLLIElement>>(new Map());
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);
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

  const rows = useBoardRows({
    visibleItems,
    linksSummary,
    stepRanks,
    familyTints,
    hoverPeerIds
  });

  const invalidateList = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: intentsQueryKeys.lists() });
  }, [queryClient]);

  const handleReorder = useCallback(
    ({ movedId, beforeId, afterId }: EntityListReorder) => {
      if (!beforeId && !afterId) return;
      // Optimistic: place a synthetic sort_key between the neighbours so the local
      // ordering matches the dropped position immediately. The realtime event with
      // the authoritative key arrives later and replaces this placeholder.
      const placeholder = synthesizeSortKey(allItems, beforeId, afterId);
      queryClient.setQueriesData<IntentListInfinite>(
        { queryKey: intentsQueryKeys.lists() },
        (prev) => {
          if (!prev) return prev;
          const pages = prev.pages.map((page) => {
            const idx = page.items.findIndex((it) => it.id === movedId);
            if (idx < 0) return page;
            const next = page.items.slice();
            next[idx] = { ...next[idx], sort_key: placeholder };
            return { ...page, items: next };
          });
          const changed = pages.some((page, i) => page !== prev.pages[i]);
          return changed ? { ...prev, pages } : prev;
        }
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

  const handleReachEnd = useCallback(() => {
    if (hasNextPage && !isFetchingNextPage) fetchNextPage();
  }, [fetchNextPage, hasNextPage, isFetchingNextPage]);

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
      {isSuccess && isTagContext(context) ? (
        <PinnedSection
          items={allItems}
          currentContextTagId={currentContextTagId}
          currentContextLabel={context ?? ""}
          pinnedIntents={pinnedIntents}
          onMutationFailed={invalidateList}
        />
      ) : null}
      <div ref={scrollContainerRef} className="min-h-0 flex-1 overflow-y-auto">
        {isPending && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        )}
        {loadErrorMessage !== null && (
          <p
            role="alert"
            className="m-0 px-3.5 py-4 text-[13px] text-base-content/60"
          >
            {loadErrorMessage}
          </p>
        )}
        {isSuccess && (
          <div
            ref={listLayerRef}
            className="relative"
            style={hasAnyLinks ? { paddingRight: LINKS_RAIL_WIDTH } : undefined}
          >
            <VirtualEntityList
              items={rows}
              emptyMessage={emptyMessage(context, allItems.length)}
              scrollParentRef={scrollContainerRef}
              onReorder={handleReorder}
              onRowHover={setHoveredId}
              rowRef={handleRowRef}
              onVisibleItemsChange={setVisibleVirtual}
              onReachEnd={handleReachEnd}
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
        {isFetchingNextPage && (
          <p className="m-0 px-3.5 py-2 text-[11px] text-base-content/50">
            Подгружаем…
          </p>
        )}
      </div>
    </section>
  );
}
