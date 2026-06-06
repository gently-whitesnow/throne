import { History, Pencil } from "lucide-react";
import { useState } from "react";

import {
  SCHEMA_MAP_SLUG,
  useRepositoryDocumentQuery,
  type RepositoryCoordinate
} from "@/entities/repository";
import { HttpError } from "@/shared/api";
import { MarkdownView } from "@/shared/ui";

import { CopySchemaPromptButton } from "./CopySchemaPromptButton";
import { SchemaDocumentEditor } from "./SchemaDocumentEditor";
import { SchemaDocumentVersions } from "./SchemaDocumentVersions";

interface RepositorySchemaDocumentProps {
  coordinate: RepositoryCoordinate;
  fullName: string;
}

/**
 * The repository's `db-schema-map` page: render → edit (optimistic-concurrency)
 * → history. A 404 is the normal "not generated yet" state, offering the
 * schema_map prompt plus a manual-authoring fallback.
 */
export function RepositorySchemaDocument({
  coordinate,
  fullName
}: RepositorySchemaDocumentProps) {
  const { data, isPending, error } = useRepositoryDocumentQuery(
    coordinate,
    SCHEMA_MAP_SLUG
  );
  const [editing, setEditing] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);

  const missing = error instanceof HttpError && error.status === 404;

  return (
    <section
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-100 p-5"
      aria-label="Карта схемы БД"
    >
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col gap-0.5">
          <h2 className="m-0 text-lg font-semibold">Карта схемы БД</h2>
          {data ? (
            <span className="text-[11px] text-base-content/60">
              v{data.version} · обновлено{" "}
              <time dateTime={data.updated_at}>
                {new Date(data.updated_at).toLocaleString()}
              </time>
            </span>
          ) : null}
        </div>
        {!editing ? (
          <div className="flex items-center gap-2">
            <CopySchemaPromptButton fullName={fullName} />
            {data ? (
              <>
                <button
                  type="button"
                  className="btn btn-xs btn-outline"
                  onClick={() => {
                    setHistoryOpen(true);
                  }}
                >
                  <History aria-hidden size={14} strokeWidth={2} /> История
                </button>
                <button
                  type="button"
                  className="btn btn-xs btn-primary"
                  onClick={() => {
                    setEditing(true);
                  }}
                >
                  <Pencil aria-hidden size={14} strokeWidth={2} /> Править
                </button>
              </>
            ) : null}
          </div>
        ) : null}
      </header>

      {isPending ? (
        <p className="m-0 text-sm text-base-content/60">Загрузка…</p>
      ) : editing ? (
        <SchemaDocumentEditor
          coordinate={coordinate}
          slug={SCHEMA_MAP_SLUG}
          current={data ?? null}
          onSaved={() => {
            setEditing(false);
          }}
          onCancel={() => {
            setEditing(false);
          }}
        />
      ) : missing ? (
        <EmptyState
          onCreate={() => {
            setEditing(true);
          }}
        />
      ) : error !== null ? (
        <p role="alert" className="m-0 text-sm text-error">
          Не удалось загрузить карту схемы
          {error instanceof HttpError ? ` (${String(error.status)})` : ""}.
        </p>
      ) : (
        <MarkdownView markdown={data.document} />
      )}

      {data ? (
        <SchemaDocumentVersions
          open={historyOpen}
          coordinate={coordinate}
          slug={SCHEMA_MAP_SLUG}
          onClose={() => {
            setHistoryOpen(false);
          }}
        />
      ) : null}
    </section>
  );
}

function EmptyState({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="flex flex-col items-start gap-3 rounded-md border border-dashed border-base-300 bg-base-200/40 p-5">
      <p className="m-0 text-sm text-base-content/70">
        Карта схемы ещё не сформирована. Скопируйте промпт и запустите проход
        schema_map во внешнем терминале с клоном репозитория — агент
        проанализирует код и запишет карту. Либо начните страницу вручную.
      </p>
      <button
        type="button"
        className="btn btn-sm btn-outline"
        onClick={onCreate}
      >
        <Pencil aria-hidden size={14} strokeWidth={2} /> Создать вручную
      </button>
    </div>
  );
}
