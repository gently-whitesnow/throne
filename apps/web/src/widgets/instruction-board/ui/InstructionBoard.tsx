import { Search } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  instructionKindLabel,
  type InstructionListItem
} from "@/entities/instruction";
import { HttpError, httpGet, instructionsEndpoints } from "@/shared/api";
import { EntityList, type EntityListRow } from "@/shared/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: InstructionListItem[] }
  | { kind: "error"; message: string };

export function InstructionBoard() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [query, setQuery] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    httpGet<InstructionListItem[]>(
      instructionsEndpoints.listInstructions(),
      controller.signal
    )
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить instructions (${String(err.status)}).`
            : "Не удалось загрузить instructions.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, []);

  const rows = useMemo<EntityListRow[]>(() => {
    if (state.kind !== "ready") return [];
    const q = query.trim().toLowerCase();
    return state.items
      .filter((i) => {
        if (!q) return true;
        return (
          i.kind.toLowerCase().includes(q) ||
          i.text_short.toLowerCase().includes(q)
        );
      })
      .map((i) => {
        const meta = instructionKindLabel(i.kind);
        return {
          id: i.id,
          title: meta.label,
          subtitle: firstLine(i.text_short),
          meta: `v${String(i.current_version)}`,
          badge: i.kind,
          badgeColor: meta.surface,
          href: `/instructions/${i.id}`
        };
      });
  }, [state, query]);

  return (
    <section className="master-pane" aria-label="Список Instructions">
      <div className="master-pane__header">
        <h2 className="master-pane__title">Instructions</h2>
      </div>
      <div className="master-pane__search">
        <Search aria-hidden size={14} strokeWidth={2} />
        <input
          type="search"
          placeholder="Поиск по kind и тексту"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
          }}
          aria-label="Поиск instructions"
        />
      </div>
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
            emptyMessage="Инструкции отсутствуют — seed bootstrap не отработал."
          />
        )}
      </div>
    </section>
  );
}

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}
