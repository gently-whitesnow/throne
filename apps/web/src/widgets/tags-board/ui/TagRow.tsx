import { Check, Pencil, Trash2, X } from "lucide-react";
import { useState } from "react";

import { renameTag, type Tag } from "@/entities/tag";
import { errorMessage } from "@/shared/lib";
import { normalizeTagSlug, TAG_NAME_MAX_LENGTH } from "@/shared/ui";

interface TagRowProps {
  tag: Tag;
  selected: boolean;
  /** Число привязанных интентов — едет инлайн в item'е страницы. */
  intentsCount: number;
  onSelect: (tagId: string) => void;
  onRequestDelete: (tag: Tag) => void;
}

function slugErrorText(
  reason: "empty" | "too-long" | "bad-chars" | null
): string {
  switch (reason) {
    case "empty":
      return "Имя не может быть пустым.";
    case "too-long":
      return `Не длиннее ${String(TAG_NAME_MAX_LENGTH)} символов.`;
    case "bad-chars":
      return "Только латиница, цифры, дефис и подчёркивание.";
    default:
      return "Некорректное имя тега.";
  }
}

export function TagRow({
  tag,
  selected,
  intentsCount,
  onSelect,
  onRequestDelete
}: TagRowProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(tag.name);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startEdit = () => {
    setDraft(tag.name);
    setError(null);
    setEditing(true);
  };

  const cancelEdit = () => {
    setEditing(false);
    setError(null);
  };

  const handleSubmit = (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    const result = normalizeTagSlug(draft);
    if (!result.valid) {
      setError(slugErrorText(result.reason));
      return;
    }
    if (result.slug === tag.name) {
      cancelEdit();
      return;
    }
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await renameTag(tag.id, {
          name: result.slug,
          expected_version: tag.current_version
        });
        setEditing(false);
      } catch (err: unknown) {
        setError(errorMessage(err, { base: "Не удалось переименовать" }));
      } finally {
        setBusy(false);
      }
    })();
  };

  if (editing) {
    return (
      <li className="flex flex-col gap-1 border-b border-base-300 px-3.5 py-2 last:border-b-0">
        <form onSubmit={handleSubmit} className="flex items-center gap-2">
          <span aria-hidden className="text-base-content/50">
            #
          </span>
          <input
            type="text"
            autoFocus
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              if (error !== null) setError(null);
            }}
            onKeyDown={(e) => {
              if (e.key === "Escape") cancelEdit();
            }}
            aria-label={`Новое имя для #${tag.name}`}
            disabled={busy}
            className="min-w-0 flex-1 rounded bg-base-200 px-2 py-1 text-[13px] text-base-content focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
          />
          <button
            type="submit"
            disabled={busy}
            aria-label="Сохранить"
            className="btn btn-ghost btn-xs btn-square text-success"
          >
            <Check size={15} aria-hidden />
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={cancelEdit}
            aria-label="Отмена"
            className="btn btn-ghost btn-xs btn-square"
          >
            <X size={15} aria-hidden />
          </button>
        </form>
        {error !== null && (
          <p role="alert" className="m-0 pl-4 text-[11px] text-error">
            {error}
          </p>
        )}
      </li>
    );
  }

  return (
    <li
      className={`group flex items-center gap-2 border-b border-base-300 px-3.5 py-2 last:border-b-0 ${
        selected ? "bg-primary/10" : ""
      }`}
    >
      <button
        type="button"
        aria-pressed={selected}
        onClick={() => {
          onSelect(tag.id);
        }}
        title={`#${tag.name}`}
        data-testid={`tag-row-${tag.id}`}
        className="flex min-w-0 flex-1 items-center gap-2 rounded text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
      >
        <span className="badge badge-sm badge-outline max-w-full truncate border-base-300 text-base-content/80">
          #{tag.name}
        </span>
      </button>
      <span
        title={`Привязан к ${String(intentsCount)} ${intentsCount === 1 ? "интенту" : "интентам"}`}
        className={`shrink-0 rounded-md px-1.5 text-[11px] tabular-nums ${
          intentsCount > 0
            ? "bg-base-200 text-base-content/70"
            : "text-base-content/40"
        }`}
      >
        {intentsCount}
      </span>
      <div className="flex shrink-0 items-center gap-0.5 opacity-60 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
        <button
          type="button"
          onClick={startEdit}
          aria-label={`Переименовать #${tag.name}`}
          className="btn btn-ghost btn-xs btn-square"
        >
          <Pencil size={14} aria-hidden />
        </button>
        <button
          type="button"
          onClick={() => {
            onRequestDelete(tag);
          }}
          aria-label={`Удалить #${tag.name}`}
          className="btn btn-ghost btn-xs btn-square text-base-content/60 hover:text-error"
        >
          <Trash2 size={14} aria-hidden />
        </button>
      </div>
    </li>
  );
}
