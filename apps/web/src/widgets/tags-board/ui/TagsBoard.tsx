import { Trash2, Pencil, Plus } from "lucide-react";
import { useState } from "react";

import {
  type Tag,
  createTag,
  deleteTag,
  fetchTagUsage,
  renameTag,
  useTags
} from "@/entities/tag";
import { Button } from "@/shared/ui";

interface TagsBoardProps {
  selectedTagId: string | null;
  onSelectTag: (tagId: string) => void;
}

export function TagsBoard({ selectedTagId, onSelectTag }: TagsBoardProps) {
  const tagsQuery = useTags();
  const [newName, setNewName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const handleCreate = (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    const trimmed = newName.trim();
    if (!trimmed) return;
    setCreateError(null);
    void (async () => {
      try {
        await createTag({ name: trimmed });
        setNewName("");
      } catch (err: unknown) {
        setCreateError(
          err instanceof Error ? err.message : "Не удалось создать тег."
        );
      }
    })();
  };

  const handleRename = async (tag: Tag) => {
    const proposed = window.prompt(`Новое имя для #${tag.name}`, tag.name);
    if (proposed === null) return;
    const trimmed = proposed.trim();
    if (!trimmed || trimmed === tag.name) return;
    setBusyId(tag.id);
    try {
      await renameTag(tag.id, {
        name: trimmed,
        expected_version: tag.current_version
      });
    } catch (err: unknown) {
      window.alert(
        err instanceof Error ? err.message : "Не удалось переименовать."
      );
    } finally {
      setBusyId(null);
    }
  };

  const handleDelete = async (tag: Tag) => {
    setBusyId(tag.id);
    try {
      const usage = await fetchTagUsage(tag.id);
      const detach = usage.intents_count > 0;
      const message = detach
        ? `Тег #${tag.name} назначен ${String(usage.intents_count)} intent'у/ам. Открепить и удалить?`
        : `Удалить тег #${tag.name}?`;
      if (!window.confirm(message)) return;
      await deleteTag(tag.id, detach);
    } catch (err: unknown) {
      window.alert(err instanceof Error ? err.message : "Не удалось удалить.");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section
      className="flex min-w-0 flex-col border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Список тегов"
    >
      <div className="flex items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          Tags
        </h2>
      </div>
      <form
        className="flex items-center gap-2 border-b border-base-300 px-3.5 py-2 text-base-content/60"
        onSubmit={handleCreate}
      >
        <Plus aria-hidden size={14} strokeWidth={2} />
        <input
          type="text"
          placeholder="Новый тег (slug)"
          value={newName}
          onChange={(e) => {
            setNewName(e.target.value);
          }}
          aria-label="Имя нового тега"
          className="min-w-0 flex-1 bg-transparent py-1 text-[13px] text-base-content placeholder:text-base-content/50 focus:outline-none"
        />
        <Button type="submit" variant="primary" disabled={!newName.trim()}>
          Создать
        </Button>
      </form>
      {createError && (
        <p role="alert" className="m-0 px-3.5 py-3 text-[13px] text-error">
          {createError}
        </p>
      )}
      <div className="min-h-0 flex-1 overflow-y-auto">
        {tagsQuery.isPending && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        )}
        {tagsQuery.isError && (
          <p
            role="alert"
            className="m-0 px-3.5 py-4 text-[13px] text-base-content/60"
          >
            {tagsQuery.error instanceof Error
              ? tagsQuery.error.message
              : "Не удалось загрузить теги."}
          </p>
        )}
        {tagsQuery.isSuccess && tagsQuery.data.length === 0 && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Нет тегов.
          </p>
        )}
        {tagsQuery.isSuccess && tagsQuery.data.length > 0 && (
          <ul className="m-0 flex list-none flex-col p-0">
            {tagsQuery.data.map((tag) => {
              const selected = tag.id === selectedTagId;
              return (
                <li
                  key={tag.id}
                  className={
                    selected
                      ? "flex items-center gap-2 border-b border-base-300 bg-primary/10 px-3.5 py-2 last:border-b-0"
                      : "flex items-center gap-2 border-b border-base-300 px-3.5 py-2 last:border-b-0"
                  }
                >
                  <button
                    type="button"
                    aria-pressed={selected}
                    onClick={() => {
                      onSelectTag(tag.id);
                    }}
                    className="flex min-w-0 flex-1 items-center gap-2 text-left focus:outline-none"
                    data-testid={`tag-row-${tag.id}`}
                  >
                    <span className="badge badge-sm badge-outline border-base-300 text-base-content/80">
                      #{tag.name}
                    </span>
                    <span className="text-[11px] tabular-nums text-base-content/60">
                      v{String(tag.current_version)}
                    </span>
                  </button>
                  <div className="ml-auto flex gap-1">
                    <Button
                      variant="default"
                      onClick={() => void handleRename(tag)}
                      disabled={busyId === tag.id}
                      aria-label="Переименовать"
                    >
                      <Pencil size={14} aria-hidden /> Rename
                    </Button>
                    <Button
                      variant="default"
                      onClick={() => void handleDelete(tag)}
                      disabled={busyId === tag.id}
                      aria-label="Удалить"
                    >
                      <Trash2 size={14} aria-hidden /> Delete
                    </Button>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </section>
  );
}
