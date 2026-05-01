import { Search } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import type { IntentListItem } from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
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

  const reload = () => {
    setReloadKey((v) => v + 1);
  };

  const allTags = useMemo(() => {
    if (state.kind !== "ready") return [] as string[];
    const set = new Set<string>();
    for (const i of state.items) for (const t of i.tags) set.add(t);
    return [...set].sort();
  }, [state]);

  const rows = useMemo<EntityListRow[]>(() => {
    if (state.kind !== "ready") return [];
    const q = query.trim().toLowerCase();
    return state.items
      .filter((i) => {
        if (activeTag && !i.tags.includes(activeTag)) return false;
        if (!q) return true;
        return (
          i.text_short.toLowerCase().includes(q) ||
          i.tags.some((t) => t.toLowerCase().includes(q))
        );
      })
      .map((i) => ({
        id: i.id,
        title: firstLine(i.text_short) || i.id,
        subtitle: i.tags.length > 0 ? `#${i.tags.join(" #")}` : undefined,
        meta: `v${String(i.current_version)}`,
        href: `/intents/${i.id}`
      }));
  }, [state, query, activeTag]);

  return (
    <section className="master-pane" aria-label="Список Intents">
      <div className="master-pane__header">
        <h2 className="master-pane__title">Intents</h2>
        <CreateIntentButton
          onCreated={(intent) => {
            reload();
            void navigate(`/intents/${intent.id}`);
          }}
        />
      </div>
      <div className="master-pane__search">
        <Search aria-hidden size={14} strokeWidth={2} />
        <input
          type="search"
          placeholder="Поиск по тексту и тегам"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
          }}
          aria-label="Поиск intents"
        />
      </div>
      {allTags.length > 0 && (
        <div
          className="master-pane__tags"
          role="group"
          aria-label="Фильтр по тегам"
        >
          {allTags.map((tag) => {
            const active = activeTag === tag;
            return (
              <button
                key={tag}
                type="button"
                className={`tag-chip${active ? " tag-chip--active" : ""}`}
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
      <div className="master-pane__body">
        {state.kind === "loading" && (
          <p className="master-pane__hint">Загрузка…</p>
        )}
        {state.kind === "error" && (
          <p role="alert" className="master-pane__hint">
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

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}
