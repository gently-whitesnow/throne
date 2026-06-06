import { X } from "lucide-react";
import { useEffect, useState } from "react";

import {
  useRepositoryDocumentVersionsQuery,
  type RepositoryCoordinate,
  type RepositoryDocumentVersion
} from "@/entities/repository";
import { MarkdownView } from "@/shared/ui";

interface SchemaDocumentVersionsProps {
  open: boolean;
  coordinate: RepositoryCoordinate;
  slug: string;
  onClose: () => void;
}

/**
 * Read-only history of a knowledge page. Versions are full snapshots
 * (ASC from the API); selecting one renders its body. No restore — reverting
 * is out of scope for the slice (copy the body back into the editor by hand).
 */
export function SchemaDocumentVersions({
  open,
  coordinate,
  slug,
  onClose
}: SchemaDocumentVersionsProps) {
  const { data, isPending, error } = useRepositoryDocumentVersionsQuery(
    coordinate,
    slug,
    open
  );
  const [selected, setSelected] = useState<number | null>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
    };
  }, [open, onClose]);

  const versions: RepositoryDocumentVersion[] = data
    ? [...data].sort((a, b) => b.version - a.version)
    : [];
  const active =
    versions.length > 0
      ? (versions.find((v) => v.version === selected) ?? versions[0])
      : null;

  return (
    <>
      <div
        className={`fixed inset-0 z-40 bg-neutral/40 transition-opacity duration-200 ${
          open ? "opacity-100" : "pointer-events-none opacity-0"
        }`}
        onClick={onClose}
        aria-hidden
      />
      <aside
        className={`fixed inset-y-0 right-0 z-50 flex w-[min(640px,100vw)] flex-col border-l border-base-300 bg-base-100 transition-transform duration-200 ${
          open ? "translate-x-0" : "translate-x-full"
        }`}
        aria-hidden={!open}
        aria-label="История версий карты схемы"
      >
        <header className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-base-300 px-4 py-3">
          <h3 className="m-0 text-sm font-semibold">История версий</h3>
          <button
            type="button"
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
          {open ? (
            <Body
              isPending={isPending}
              hasError={error !== null}
              versions={versions}
              active={active}
              onSelect={setSelected}
            />
          ) : null}
        </div>
      </aside>
    </>
  );
}

interface BodyProps {
  isPending: boolean;
  hasError: boolean;
  versions: RepositoryDocumentVersion[];
  active: RepositoryDocumentVersion | null;
  onSelect: (version: number) => void;
}

function Body({ isPending, hasError, versions, active, onSelect }: BodyProps) {
  if (isPending) {
    return <p className="text-sm text-base-content/60">История загружается…</p>;
  }
  if (hasError) {
    return (
      <p role="alert" className="text-sm text-error">
        Не удалось загрузить историю.
      </p>
    );
  }
  if (versions.length === 0) {
    return <p className="text-sm text-base-content/60">Истории пока нет.</p>;
  }
  return (
    <div className="flex flex-col gap-3">
      <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
        {versions.map((v) => {
          const isActive = active?.version === v.version;
          return (
            <li key={v.version} className="m-0 p-0">
              <button
                type="button"
                onClick={() => {
                  onSelect(v.version);
                }}
                aria-pressed={isActive}
                className={`rounded-md border px-2.5 py-1 text-xs transition-colors ${
                  isActive
                    ? "border-primary/60 bg-primary/10 text-primary"
                    : "border-base-300 bg-base-100 hover:bg-base-200"
                }`}
                title={new Date(v.created_at).toLocaleString()}
              >
                v{v.version}
              </button>
            </li>
          );
        })}
      </ul>
      {active ? (
        <article className="rounded-md border border-base-300 bg-base-100 p-4">
          <header className="mb-2 flex flex-wrap items-baseline gap-2 border-b border-base-300 pb-2">
            <strong className="text-sm">{active.title}</strong>
            <span className="text-[11px] text-base-content/60">
              v{active.version} ·{" "}
              <time dateTime={active.created_at}>
                {new Date(active.created_at).toLocaleString()}
              </time>
            </span>
          </header>
          <MarkdownView markdown={active.document} />
        </article>
      ) : null}
    </div>
  );
}
