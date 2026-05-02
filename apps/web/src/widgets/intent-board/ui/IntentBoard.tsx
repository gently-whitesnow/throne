import { Search } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import {
  intentStatusMeta,
  intentStatusOrder,
  type IntentListItem,
  type IntentStatus
} from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";
import { EntityList, type EntityListRow } from "@/shared/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: IntentListItem[] }
  | { kind: "error"; message: string };

export function IntentBoard() {
  const navigate = useNavigate();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [query, setQuery] = useState("");
  const [activeTag, setActiveTag] = useState<string | null>(null);
  const [activeStatus, setActiveStatus] = useState<IntentStatus | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    httpGet<IntentListItem[]>(intentsEndpoints.listIntents(), controller.signal)
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить intents (${String(err.status)}).`
            : "Не удалось загрузить intents.";
        setState({ kind: "error", message });
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

  const allTags = useMemo(() => {
    if (state.kind !== "ready") return [] as string[];
    const set = new Set<string>();
    for (const i of state.items) for (const t of i.tags) set.add(t.name);
    return [...set].sort();
  }, [state]);

  const rows = useMemo<EntityListRow[]>(() => {
    if (state.kind !== "ready") return [];
    const q = query.trim().toLowerCase();
    return state.items
      .filter((i) => {
        if (activeTag && !i.tags.some((t) => t.name === activeTag))
          return false;
        if (activeStatus && i.status !== activeStatus) return false;
        if (!q) return true;
        return (
          i.text_short.toLowerCase().includes(q) ||
          i.tags.some((t) => t.name.toLowerCase().includes(q))
        );
      })
      .map((i) => {
        const status = intentStatusMeta[i.status];
        const tagNames = i.tags.map((t) => t.name);
        return {
          id: i.id,
          title: firstLine(i.text_short) || i.id,
          subtitle: tagNames.length > 0 ? `#${tagNames.join(" #")}` : undefined,
          meta: `v${String(i.current_version)}`,
          badge: status.label,
          badgeColor: status.surface,
          badgeTextColor: status.ink,
          href: `/intents/${i.id}`
        };
      });
  }, [state, query, activeTag, activeStatus]);

  useRealtimeEvent("intent.tags_changed", reload);

  return (
    <section
      className="flex min-w-0 flex-col border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Список Intents"
    >
      <div className="flex items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          Intents
        </h2>
        <CreateIntentButton
          onCreated={(intent) => {
            reload();
            void navigate(`/intents/${intent.id}`);
          }}
        />
      </div>
      <div className="flex items-center gap-2 border-b border-base-300 px-3.5 py-2 text-base-content/60">
        <Search aria-hidden size={14} strokeWidth={2} />
        <input
          type="search"
          placeholder="Поиск по тексту и тегам"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
          }}
          aria-label="Поиск intents"
          className="min-w-0 flex-1 bg-transparent py-1 text-[13px] text-base-content placeholder:text-base-content/50 focus:outline-none"
        />
      </div>
      {allTags.length > 0 && (
        <div
          className="flex flex-wrap gap-1 border-b border-base-300 px-3.5 py-2"
          role="group"
          aria-label="Фильтр по тегам"
        >
          {allTags.map((tag) => {
            const active = activeTag === tag;
            return (
              <button
                key={tag}
                type="button"
                className={chipClass(active)}
                onClick={() => {
                  setActiveTag(active ? null : tag);
                }}
              >
                #{tag}
              </button>
            );
          })}
        </div>
      )}
      <div
        className="flex flex-wrap gap-1 border-b border-base-300 px-3.5 py-2"
        role="group"
        aria-label="Фильтр по статусу"
      >
        {intentStatusOrder.map((status) => {
          const active = activeStatus === status;
          return (
            <button
              key={status}
              type="button"
              className={chipClass(active)}
              onClick={() => {
                setActiveStatus(active ? null : status);
              }}
            >
              {intentStatusMeta[status].label}
            </button>
          );
        })}
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
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
          <EntityList
            items={rows}
            emptyMessage="Нет intents. Создайте первый."
          />
        )}
      </div>
    </section>
  );
}

function chipClass(active: boolean): string {
  const base =
    "inline-flex h-[22px] items-center rounded-full border px-2 text-[11px] font-medium transition-colors cursor-pointer";
  return active
    ? `${base} border-primary bg-primary/10 text-primary`
    : `${base} border-base-300 bg-base-100 text-base-content/70 hover:bg-base-200 hover:text-base-content`;
}

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}
