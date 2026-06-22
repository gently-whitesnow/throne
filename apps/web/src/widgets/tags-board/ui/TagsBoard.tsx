import { Plus, Search } from "lucide-react";
import { useMemo, useState } from "react";

import {
  type Tag,
  createTag,
  useTagUsages,
  useTags
} from "@/entities/tag";
import { errorMessage } from "@/shared/lib";
import { Button, normalizeTagSlug, TAG_NAME_MAX_LENGTH } from "@/shared/ui";

import { DeleteTagDialog } from "./DeleteTagDialog";
import { TagRow } from "./TagRow";

interface TagsBoardProps {
  selectedTagId: string | null;
  onSelectTag: (tagId: string | null) => void;
}

export function TagsBoard({ selectedTagId, onSelectTag }: TagsBoardProps) {
  const tagsQuery = useTags();
  const [newName, setNewName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<Tag | null>(null);

  const tags = useMemo(() => tagsQuery.data ?? [], [tagsQuery.data]);
  const tagIds = useMemo(() => tags.map((tag) => tag.id), [tags]);
  const usageByTag = useTagUsages(tagIds);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return tags;
    return tags.filter((tag) => tag.name.toLowerCase().includes(q));
  }, [tags, search]);

  const createSlug = normalizeTagSlug(newName);
  const canCreate = createSlug.valid;

  const handleCreate = (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!createSlug.valid) return;
    setCreateError(null);
    void (async () => {
      try {
        await createTag({ name: createSlug.slug });
        setNewName("");
      } catch (err: unknown) {
        setCreateError(errorMessage(err, { base: "Не удалось создать тег" }));
      }
    })();
  };

  const handleDeleted = (tagId: string) => {
    if (selectedTagId === tagId) onSelectTag(null);
  };

  const hasTags = tagsQuery.isSuccess && tags.length > 0;

  return (
    <section
      className="flex min-w-0 flex-col border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Список тегов"
    >
      <div className="flex items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          Теги
        </h2>
        {hasTags && (
          <span className="text-[12px] tabular-nums text-base-content/40">
            {String(tags.length)}
          </span>
        )}
      </div>

      {hasTags && (
        <div className="flex items-center gap-2 border-b border-base-300 px-3.5 py-2 text-base-content/60 focus-within:text-base-content">
          <Search aria-hidden size={14} strokeWidth={2} />
          <input
            type="search"
            placeholder="Поиск по тегам…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
            }}
            aria-label="Поиск по тегам"
            className="min-w-0 flex-1 bg-transparent py-1 text-[13px] text-base-content placeholder:text-base-content/50 focus:outline-none"
          />
        </div>
      )}

      <form
        className="flex items-center gap-2 border-b border-base-300 px-3.5 py-2 text-base-content/60 focus-within:text-base-content"
        onSubmit={handleCreate}
      >
        <Plus aria-hidden size={14} strokeWidth={2} />
        <input
          type="text"
          placeholder="Новый тег (slug)"
          value={newName}
          onChange={(e) => {
            setNewName(e.target.value);
            if (createError !== null) setCreateError(null);
          }}
          maxLength={TAG_NAME_MAX_LENGTH + 8}
          aria-label="Имя нового тега"
          className="min-w-0 flex-1 bg-transparent py-1 text-[13px] text-base-content placeholder:text-base-content/50 focus:outline-none"
        />
        <Button type="submit" variant="primary" disabled={!canCreate}>
          Создать
        </Button>
      </form>
      {newName.trim() !== "" && !canCreate && createError === null && (
        <p className="m-0 px-3.5 py-1.5 text-[11px] text-base-content/50">
          {createSlug.reason === "too-long"
            ? `Не длиннее ${String(TAG_NAME_MAX_LENGTH)} символов.`
            : "Только латиница, цифры, дефис и подчёркивание."}
        </p>
      )}
      {createError !== null && (
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
            {errorMessage(tagsQuery.error, {
              base: "Не удалось загрузить теги"
            })}
          </p>
        )}
        {tagsQuery.isSuccess && tags.length === 0 && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Нет тегов. Создайте первый выше.
          </p>
        )}
        {hasTags && filtered.length === 0 && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Ничего не найдено по «{search.trim()}».
          </p>
        )}
        {hasTags && filtered.length > 0 && (
          <ul className="m-0 flex list-none flex-col p-0">
            {filtered.map((tag) => (
              <TagRow
                key={tag.id}
                tag={tag}
                selected={tag.id === selectedTagId}
                intentsCount={usageByTag.get(tag.id)}
                onSelect={onSelectTag}
                onRequestDelete={setDeleteTarget}
              />
            ))}
          </ul>
        )}
      </div>

      {deleteTarget !== null && (
        <DeleteTagDialog
          tag={deleteTarget}
          onClose={() => {
            setDeleteTarget(null);
          }}
          onDeleted={handleDeleted}
        />
      )}
    </section>
  );
}
