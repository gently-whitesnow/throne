import { Search } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import {
  compareSortKeys,
  intentStatusMeta,
  type IntentListItem
} from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { moveIntent } from "@/features/move-intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";
import { type EntityListReorder, type EntityListRow } from "@/shared/ui";

import {
  contextTitle,
  emptyMessage,
  firstLine,
  matchesContext,
  synthesizeSortKey
} from "../model/board-helpers";
import { buildEntries, type ClusterMove } from "../model/board-dnd";
import { computeFamilyTints } from "../model/family-tint";
import { computeStepRanks } from "../model/step-rank";
import {
  computeClusters,
  useClusterCollapsedState
} from "../model/useClusters";
import { useLinksSummary } from "../model/useLinksSummary";
import { IntentBoardList } from "./IntentBoardList";
import { IntentLinksOverlay } from "./IntentLinksOverlay";
import { LinkChips } from "./LinkChips";

const LINKS_RAIL_WIDTH = 36;

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: IntentListItem[] }
  | { kind: "error"; message: string };

export function IntentBoard() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [query, setQuery] = useState("");

  const context = params.get("context");

  useEffect(() => {
    const controller = new AbortController();
    httpGet<IntentListItem[]>(intentsEndpoints.listIntents(), controller.signal)
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof HttpError
              ? `Не удалось загрузить intents (${String(err.status)}).`
              : "Не удалось загрузить intents."
        });
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

  const onReordered = useCallback(
    (payload: { intent_id: string; sort_key: string }) => {
      setState((prev) => {
        if (prev.kind !== "ready") return prev;
        return {
          ...prev,
          items: prev.items.map((i) =>
            i.id === payload.intent_id
              ? { ...i, sort_key: payload.sort_key }
              : i
          )
        };
      });
    },
    []
  );
  useRealtimeEvent("intent.reordered", onReordered);

  const orderedItems = useMemo(
    () =>
      state.kind === "ready"
        ? [...state.items].sort((a, b) =>
            compareSortKeys(a.sort_key, b.sort_key)
          )
        : [],
    [state]
  );

  const visibleItems = useMemo(() => {
    if (state.kind !== "ready") return [] as IntentListItem[];
    const q = query.trim().toLowerCase();
    return orderedItems
      .filter((i) => matchesContext(i, context))
      .filter((i) => {
        if (!q) return true;
        return (
          i.text_short.toLowerCase().includes(q) ||
          i.tags.some((t) => t.name.toLowerCase().includes(q))
        );
      });
  }, [context, orderedItems, query, state.kind]);

  const visibleIds = useMemo(
    () => visibleItems.map((i) => i.id),
    [visibleItems]
  );
  const linksSummary = useLinksSummary(visibleIds);
  const layoutSignature = useMemo(() => visibleIds.join("|"), [visibleIds]);
  const stepRanks = useMemo(
    () => computeStepRanks(visibleIds, linksSummary),
    [linksSummary, visibleIds]
  );
  const familyTints = useMemo(
    () => computeFamilyTints(visibleIds, linksSummary),
    [linksSummary, visibleIds]
  );
  const clustersResult = useMemo(
    () =>
      computeClusters(
        visibleItems.map((i) => ({
          id: i.id,
          tagNames: i.tags.map((t) => t.name)
        })),
        linksSummary
      ),
    [linksSummary, visibleItems]
  );
  const entries = useMemo(
    () => buildEntries(visibleIds, clustersResult.byIntent),
    [clustersResult.byIntent, visibleIds]
  );
  const { isCollapsed, toggle } = useClusterCollapsedState();

  const [hoveredId, setHoveredId] = useState<string | null>(null);
  const rowRefs = useRef<Map<string, HTMLLIElement>>(new Map());
  const scrollRef = useRef<HTMLDivElement | null>(null);
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

  const rowsById = useMemo<ReadonlyMap<string, EntityListRow>>(() => {
    const search = new URLSearchParams(params).toString();
    const map = new Map<string, EntityListRow>();
    for (const i of visibleItems) {
      const status = intentStatusMeta[i.status];
      const tagNames = i.tags.map((t) => t.name);
      const href =
        search.length > 0 ? `/intents/${i.id}?${search}` : `/intents/${i.id}`;
      const summary = linksSummary.get(i.id);
      const blockedCount = summary?.blocked_by.length ?? 0;
      const step = stepRanks.get(i.id) ?? 1;
      const isPeer = hoverPeerIds.has(i.id);
      map.set(i.id, {
        id: i.id,
        title: firstLine(i.text_short) || i.id,
        subtitle: tagNames.length > 0 ? `#${tagNames.join(" #")}` : undefined,
        meta: `v${String(i.current_version)}`,
        badge: status.label,
        badgeColor: status.surface,
        badgeTextColor: status.ink,
        href,
        warning: step > 1 ? `Шаг ${String(step)}` : undefined,
        warningTitle:
          step > 1
            ? `Можно начать после ${String(step - 1)} ${pluralizeSteps(step - 1)} зависимостей (всего блокирующих здесь: ${String(blockedCount)}).`
            : undefined,
        outline: isPeer
          ? "outline outline-2 outline-primary/40 outline-offset-[-2px] z-10"
          : undefined,
        tint: familyTints.get(i.id)
      });
    }
    return map;
  }, [
    familyTints,
    hoverPeerIds,
    linksSummary,
    params,
    stepRanks,
    visibleItems
  ]);

  const trailingForRow = useCallback(
    (id: string) => (
      <LinkChips
        summary={linksSummary}
        intentId={id}
        clusterByIntent={clustersResult.byIntent}
      />
    ),
    [clustersResult.byIntent, linksSummary]
  );

  const handleCardReorder = useCallback(
    ({ movedId, beforeId, afterId }: EntityListReorder) => {
      if (!beforeId && !afterId) return;
      const items = state.kind === "ready" ? state.items : [];
      const placeholder = synthesizeSortKey(items, beforeId, afterId);
      setState((prev) => {
        if (prev.kind !== "ready") return prev;
        return {
          ...prev,
          items: prev.items.map((i) =>
            i.id === movedId ? { ...i, sort_key: placeholder } : i
          )
        };
      });
      moveIntent({ intentId: movedId, beforeId, afterId }).catch(() => {
        reload();
      });
    },
    [reload, state]
  );

  const handleClusterReorder = useCallback(
    ({ memberIds, beforeId, afterId }: ClusterMove) => {
      // Sequence moveIntent calls so members land consecutively at the target
      // boundary: each call advances the «before» cursor to the just-moved id.
      // No optimistic key here — realtime intent.reordered carries the truth.
      let cursor: string | null = beforeId;
      const chain = memberIds.reduce<Promise<unknown>>(
        (prev, id) =>
          prev.then(async () => {
            await moveIntent({
              intentId: id,
              beforeId: cursor,
              afterId
            });
            cursor = id;
          }),
        Promise.resolve()
      );
      chain.catch(() => {
        reload();
      });
    },
    [reload]
  );

  const handleCreated = (intentId: string) => {
    reload();
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
        <CreateIntentButton
          onCreated={(intent) => {
            handleCreated(intent.id);
          }}
        />
      </div>
      <div className="flex flex-shrink-0 items-center gap-2 border-b border-base-300 px-3.5 py-2 text-base-content/60">
        <Search aria-hidden size={14} strokeWidth={2} />
        <input
          type="search"
          placeholder="Поиск в контексте"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
          }}
          aria-label="Поиск intents"
          className="min-w-0 flex-1 bg-transparent py-1 text-[13px] text-base-content placeholder:text-base-content/50 focus:outline-none"
        />
      </div>
      <div ref={scrollRef} className="relative min-h-0 flex-1 overflow-y-auto">
        {state.kind === "loading" && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        )}
        {state.kind === "error" && (
          <p
            role="alert"
            className="m-0 px-3.5 py-4 text-[13px] text-base-content/60"
          >
            {state.message}
          </p>
        )}
        {state.kind === "ready" && (
          <>
            <div style={{ paddingRight: LINKS_RAIL_WIDTH }}>
              <IntentBoardList
                entries={entries}
                rowsById={rowsById}
                clusters={clustersResult.clusters}
                collapsedClusters={isCollapsed}
                toggleCluster={toggle}
                onCardReorder={handleCardReorder}
                onClusterReorder={handleClusterReorder}
                onRowHover={setHoveredId}
                rowRef={handleRowRef}
                trailingForRow={trailingForRow}
                emptyMessage={emptyMessage(context, state.items.length)}
              />
            </div>
            <IntentLinksOverlay
              hoveredId={hoveredId}
              summary={linksSummary}
              rowRefs={rowRefs.current}
              containerRef={scrollRef}
              railWidth={LINKS_RAIL_WIDTH}
              layoutSignature={layoutSignature}
              clusterByIntent={clustersResult.byIntent}
            />
          </>
        )}
      </div>
    </section>
  );
}

function pluralizeSteps(n: number): string {
  return n === 1 ? "шага" : "шагов";
}
