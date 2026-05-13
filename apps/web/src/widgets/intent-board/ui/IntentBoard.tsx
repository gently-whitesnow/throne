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
import { isTagContext } from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";
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
import { useLinksSummary } from "../model/useLinksSummary";
import { IntentLinksOverlay } from "./IntentLinksOverlay";
import { PinnedSection } from "./PinnedSection";

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
  // Pin events change the `pinned_in` field on the affected intent; the cheapest
  // path is a list refetch since this is also the source of the Pinned section.
  useRealtimeEvent("intent.pinned", reload);
  useRealtimeEvent("intent.unpinned", reload);
  useRealtimeEvent("intent.pin_moved", reload);
  // Link mutations are handled by useLinksSummary (separate cache); list does
  // not refetch on link_added/link_removed — keeps list-cache and links-cache
  // independent.

  // intent.reordered is positional only — patch sort_key in place to keep the list
  // in the new order without a full refetch (the server sends the freshly assigned key).
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

  // Server-defined order is sort_key ASC, ordinal. Use compareSortKeys (byte-wise)
  // — never localeCompare, see entities/intent/model/sortKey.ts for why.
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
    return orderedItems.filter((i) => matchesContext(i, context));
  }, [context, orderedItems, state.kind]);

  // Pinned section: only meaningful inside a tag context (a real Tag id is
  // needed to scope drop-to-pin / movePin / unpin). Pinned items are filtered
  // to those pinned in the *current* tag.
  const allItems = useMemo<IntentListItem[]>(
    () => (state.kind === "ready" ? state.items : []),
    [state]
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

  const handleReorder = useCallback(
    ({ movedId, beforeId, afterId }: EntityListReorder) => {
      if (!beforeId && !afterId) return;
      // Optimistic: place a synthetic sort_key between the neighbours so the local
      // ordering matches the dropped position immediately. The realtime event with
      // the authoritative key arrives later and replaces this placeholder.
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
        // Rollback on failure: pull a fresh authoritative list.
        reload();
      });
    },
    [reload, state]
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
      {state.kind === "ready" && isTagContext(context) ? (
        <PinnedSection
          items={allItems}
          currentContextTagId={currentContextTagId}
          currentContextLabel={context ?? ""}
          pinnedIntents={pinnedIntents}
          onMutationFailed={reload}
        />
      ) : null}
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
              <EntityList
                items={rows}
                emptyMessage={emptyMessage(context, state.items.length)}
                onReorder={handleReorder}
                onRowHover={setHoveredId}
                rowRef={handleRowRef}
              />
            </div>
            <IntentLinksOverlay
              hoveredId={hoveredId}
              summary={linksSummary}
              rowRefs={rowRefs.current}
              containerRef={scrollRef}
              railWidth={LINKS_RAIL_WIDTH}
              layoutSignature={layoutSignature}
            />
          </>
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
