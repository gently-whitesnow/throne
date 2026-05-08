import { Archive, Hash, Inbox } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";

import type { IntentListItem } from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import {
  ARCHIVE_CONTEXT,
  UNTAGGED_CONTEXT,
  archiveSubContext,
  isArchiveContext
} from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";

const ARCHIVE_STATUSES = new Set(["done", "reject"] as const);

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: IntentListItem[] }
  | { kind: "error"; message: string };

interface ContextRow {
  key: string;
  label: string;
  count: number;
  icon: "tag" | "untagged" | "archive";
}

export function IntentContextRail() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [params, setParams] = useSearchParams();

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
              ? `Не удалось загрузить контексты (${String(err.status)}).`
              : "Не удалось загрузить контексты."
        });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  useRealtimeEvent("intent.created", reload);
  useRealtimeEvent("intent.deleted", reload);
  useRealtimeEvent("intent.tags_changed", reload);
  useRealtimeEvent("intent.status_changed", reload);

  const {
    tagRows,
    untaggedCount,
    archiveCount,
    archiveTagRows,
    archiveUntaggedCount
  } = useMemo(() => {
    if (state.kind !== "ready") {
      return {
        tagRows: [] as ContextRow[],
        untaggedCount: 0,
        archiveCount: 0,
        archiveTagRows: [] as ContextRow[],
        archiveUntaggedCount: 0
      };
    }
    const counts = new Map<string, number>();
    const archiveCounts = new Map<string, number>();
    let untagged = 0;
    let archive = 0;
    let archiveUntagged = 0;
    for (const item of state.items) {
      const isArchive = ARCHIVE_STATUSES.has(item.status as "done" | "reject");
      if (isArchive) {
        archive += 1;
        if (item.tags.length === 0) {
          archiveUntagged += 1;
        } else {
          for (const tag of item.tags) {
            archiveCounts.set(tag.name, (archiveCounts.get(tag.name) ?? 0) + 1);
          }
        }
        continue;
      }
      if (item.tags.length === 0) {
        untagged += 1;
        continue;
      }
      for (const tag of item.tags) {
        counts.set(tag.name, (counts.get(tag.name) ?? 0) + 1);
      }
    }
    const sortRows = (entries: Iterable<[string, number]>): ContextRow[] =>
      [...entries]
        .map(([name, count]) => ({
          key: name,
          label: name,
          count,
          icon: "tag" as const
        }))
        .sort((a, b) => {
          if (b.count !== a.count) return b.count - a.count;
          return a.label.localeCompare(b.label);
        });
    return {
      tagRows: sortRows(counts.entries()),
      untaggedCount: untagged,
      archiveCount: archive,
      archiveTagRows: sortRows(archiveCounts.entries()),
      archiveUntaggedCount: archiveUntagged
    };
  }, [state]);

  const currentContext = params.get("context");

  // Auto-pick a default context once data is available.
  useEffect(() => {
    if (state.kind !== "ready") return;
    if (currentContext) return;
    let next: string | null = null;
    if (tagRows.length > 0) next = tagRows[0].key;
    else if (untaggedCount > 0) next = UNTAGGED_CONTEXT;
    else if (archiveCount > 0) next = ARCHIVE_CONTEXT;
    if (!next) return;
    const nextParams = new URLSearchParams(params);
    nextParams.set("context", next);
    setParams(nextParams, { replace: true });
  }, [
    archiveCount,
    currentContext,
    params,
    setParams,
    state.kind,
    tagRows,
    untaggedCount
  ]);

  const select = (key: string) => {
    const nextParams = new URLSearchParams(params);
    nextParams.set("context", key);
    setParams(nextParams);
  };

  const totalActive =
    tagRows.reduce((acc, row) => acc + row.count, 0) + untaggedCount;

  return (
    <aside
      className="flex min-h-0 min-w-0 flex-col overflow-hidden border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Контексты Intents"
    >
      <div className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          Контексты
        </h2>
        <span className="text-[11px] tabular-nums text-base-content/60">
          {String(totalActive)}
        </span>
      </div>
      <nav
        className="min-h-0 flex-1 overflow-y-auto py-1"
        aria-label="Список контекстов"
      >
        {state.kind === "loading" ? (
          <p className="m-0 px-3.5 py-3 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        ) : null}
        {state.kind === "error" ? (
          <p
            role="alert"
            className="m-0 px-3.5 py-3 text-[13px] text-base-content/60"
          >
            {state.message}
          </p>
        ) : null}
        {state.kind === "ready" ? (
          <ul className="m-0 flex list-none flex-col p-0">
            {tagRows.map((row) => (
              <li key={row.key}>
                <RailRow
                  label={`#${row.label}`}
                  icon={<Hash aria-hidden size={14} strokeWidth={2} />}
                  count={row.count}
                  active={currentContext === row.key}
                  onSelect={() => {
                    select(row.key);
                  }}
                />
              </li>
            ))}
            {untaggedCount > 0 ? (
              <li className="mt-1 border-t border-base-300 pt-1">
                <RailRow
                  label="Без тегов"
                  icon={<Inbox aria-hidden size={14} strokeWidth={2} />}
                  count={untaggedCount}
                  active={currentContext === UNTAGGED_CONTEXT}
                  onSelect={() => {
                    select(UNTAGGED_CONTEXT);
                  }}
                  muted
                />
              </li>
            ) : null}
            <li
              className={
                untaggedCount > 0 ? "" : "mt-1 border-t border-base-300 pt-1"
              }
            >
              <RailRow
                label="Архив"
                icon={<Archive aria-hidden size={14} strokeWidth={2} />}
                count={archiveCount}
                active={currentContext === ARCHIVE_CONTEXT}
                onSelect={() => {
                  select(ARCHIVE_CONTEXT);
                }}
                muted
              />
            </li>
            {isArchiveContext(currentContext) && archiveCount > 0
              ? [
                  ...archiveTagRows.map((row) => (
                    <li key={`archive-${row.key}`}>
                      <RailRow
                        label={`#${row.label}`}
                        icon={<Hash aria-hidden size={14} strokeWidth={2} />}
                        count={row.count}
                        active={currentContext === archiveSubContext(row.key)}
                        onSelect={() => {
                          select(archiveSubContext(row.key));
                        }}
                        muted
                        nested
                      />
                    </li>
                  )),
                  archiveUntaggedCount > 0 ? (
                    <li key="archive-untagged">
                      <RailRow
                        label="Без тегов"
                        icon={<Inbox aria-hidden size={14} strokeWidth={2} />}
                        count={archiveUntaggedCount}
                        active={
                          currentContext === archiveSubContext(UNTAGGED_CONTEXT)
                        }
                        onSelect={() => {
                          select(archiveSubContext(UNTAGGED_CONTEXT));
                        }}
                        muted
                        nested
                      />
                    </li>
                  ) : null
                ]
              : null}
            {tagRows.length === 0 &&
            untaggedCount === 0 &&
            archiveCount === 0 ? (
              <li className="px-3.5 py-3 text-[12px] text-base-content/60">
                Пока нет intents. Создайте первый — он определит контекст.
              </li>
            ) : null}
          </ul>
        ) : null}
      </nav>
    </aside>
  );
}

interface RailRowProps {
  label: string;
  icon: React.ReactNode;
  count: number;
  active: boolean;
  onSelect: () => void;
  muted?: boolean;
  nested?: boolean;
}

function RailRow({
  label,
  icon,
  count,
  active,
  onSelect,
  muted,
  nested
}: RailRowProps) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-current={active ? "true" : undefined}
      className={[
        "flex w-full items-center gap-2 border-l-[3px] py-1.5 text-left text-[13px] transition-colors",
        nested ? "pl-8 pr-3.5" : "px-3.5",
        active
          ? "border-primary bg-primary/10 font-semibold text-primary"
          : muted
            ? "border-transparent text-base-content/70 hover:bg-base-200"
            : "border-transparent text-base-content hover:bg-base-200"
      ].join(" ")}
    >
      <span
        className={
          active
            ? "text-primary"
            : muted
              ? "text-base-content/50"
              : "text-base-content/70"
        }
      >
        {icon}
      </span>
      <span className="min-w-0 flex-1 truncate">{label}</span>
      <span
        className={[
          "tabular-nums text-[11px]",
          active ? "text-primary/80" : "text-base-content/40"
        ].join(" ")}
      >
        {String(count)}
      </span>
    </button>
  );
}
