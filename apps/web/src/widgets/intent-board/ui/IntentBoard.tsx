import { Search } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import {
  intentStatusMeta,
  type IntentListItem,
  type IntentStatus
} from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import {
  UNTAGGED_CONTEXT,
  archiveContextTag,
  isArchiveContext
} from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";
import { EntityList, type EntityListRow } from "@/shared/ui";

const ARCHIVE_STATUSES: ReadonlySet<IntentStatus> = new Set(["done", "reject"]);

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
  const contextTagName =
    context && !isArchiveContext(context) && context !== UNTAGGED_CONTEXT
      ? context
      : null;
  const initialTags = useMemo(
    () => (contextTagName ? [contextTagName] : []),
    [contextTagName]
  );

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

  const rows = useMemo<EntityListRow[]>(() => {
    if (state.kind !== "ready") return [];
    const q = query.trim().toLowerCase();
    const search = new URLSearchParams(params).toString();
    return state.items
      .filter((i) => matchesContext(i, context))
      .filter((i) => {
        if (!q) return true;
        return (
          i.text_short.toLowerCase().includes(q) ||
          i.tags.some((t) => t.name.toLowerCase().includes(q))
        );
      })
      .map((i) => {
        const status = intentStatusMeta[i.status];
        const tagNames = i.tags.map((t) => t.name);
        const href =
          search.length > 0 ? `/intents/${i.id}?${search}` : `/intents/${i.id}`;
        return {
          id: i.id,
          title: firstLine(i.text_short) || i.id,
          subtitle: tagNames.length > 0 ? `#${tagNames.join(" #")}` : undefined,
          meta: `v${String(i.current_version)}`,
          badge: status.label,
          badgeColor: status.surface,
          badgeTextColor: status.ink,
          href
        };
      });
  }, [context, params, query, state]);

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
          initialTags={initialTags}
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
            emptyMessage={emptyMessage(context, state.items.length)}
          />
        )}
      </div>
    </section>
  );
}

function matchesContext(item: IntentListItem, context: string | null): boolean {
  if (!context) return false;
  if (isArchiveContext(context)) {
    if (!ARCHIVE_STATUSES.has(item.status)) return false;
    const subTag = archiveContextTag(context);
    if (subTag === null) return true;
    if (subTag === UNTAGGED_CONTEXT) return item.tags.length === 0;
    return item.tags.some((t) => t.name === subTag);
  }
  if (ARCHIVE_STATUSES.has(item.status)) return false;
  if (context === UNTAGGED_CONTEXT) {
    return item.tags.length === 0;
  }
  return item.tags.some((t) => t.name === context);
}

function contextTitle(context: string | null): string {
  if (!context) return "Intents";
  if (isArchiveContext(context)) {
    const subTag = archiveContextTag(context);
    if (subTag === null) return "Архив";
    if (subTag === UNTAGGED_CONTEXT) return "Архив · Без тегов";
    return `Архив · # ${subTag}`;
  }
  if (context === UNTAGGED_CONTEXT) return "Без тегов";
  return `# ${context}`;
}

function emptyMessage(context: string | null, total: number): string {
  if (!context) {
    return total === 0
      ? "Нет ни одного intent. Создайте первый."
      : "Выберите контекст слева.";
  }
  if (isArchiveContext(context)) return "В архиве пусто.";
  if (context === UNTAGGED_CONTEXT) {
    return "Все active intents уже разнесены по тегам.";
  }
  return "В этом контексте пока пусто.";
}

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}
