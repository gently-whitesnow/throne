import { Plus, Search } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";

import { type Tag, createTag, useInfiniteTags } from "@/entities/tag";
import { errorMessage, useDebouncedValue } from "@/shared/lib";
import { Button, normalizeTagSlug, TAG_NAME_MAX_LENGTH } from "@/shared/ui";

import { DeleteTagDialog } from "./DeleteTagDialog";
import { TagRow } from "./TagRow";

interface TagsBoardProps {
  selectedTagId: string | null;
  onSelectTag: (tagId: string | null) => void;
}

const SEARCH_DEBOUNCE_MS = 250;

export function TagsBoard({ selectedTagId, onSelectTag }: TagsBoardProps) {
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search.trim(), SEARCH_DEBOUNCE_MS);

  const tagsQuery = useInfiniteTags({ search: debouncedSearch });
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = tagsQuery;

  const [newName, setNewName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Tag | null>(null);

  const tags = useMemo(
    () => tagsQuery.data?.pages.flatMap((p) => p.items) ?? [],
    [tagsQuery.data]
  );

  const activeSearch = debouncedSearch.length > 0;
  const hasResults = tags.length > 0;
  // Прячем поиск только на «холодном» пустом борде; при активном запросе вход
  // остаётся, даже если выдача пуста.
  const showSearch = hasResults || activeSearch;

  const sentinelRef = useRef<HTMLLIElement | null>(null);
  useEffect(() => {
    const node = sentinelRef.current;
    if (!node || !hasNextPage) return;
    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting && !isFetchingNextPage) {
        void fetchNextPage();
      }
    });
    observer.observe(node);
    return () => {
      observer.disconnect();
    };
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, tags.length]);

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

  return (
    <section
      className="flex min-w-0 flex-col border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Список тегов"
    >
      <div className="flex items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          Теги
        </h2>
        {hasResults && (
          <span className="text-[12px] tabular-nums text-base-content/40">
            {String(tags.length)}
            {hasNextPage ? "+" : ""}
          </span>
        )}
      </div>

      {showSearch && (
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
        {tagsQuery.isSuccess && !hasResults && !activeSearch && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Нет тегов. Создайте первый выше.
          </p>
        )}
        {tagsQuery.isSuccess && !hasResults && activeSearch && (
          <p className="m-0 px-3.5 py-4 text-[13px] text-base-content/60">
            Ничего не найдено по «{debouncedSearch}».
          </p>
        )}
        {hasResults && (
          <ul className="m-0 flex list-none flex-col p-0">
            {tags.map((tag) => (
              <TagRow
                key={tag.id}
                tag={tag}
                selected={tag.id === selectedTagId}
                intentsCount={tag.intents_count}
                onSelect={onSelectTag}
                onRequestDelete={setDeleteTarget}
              />
            ))}
            <li ref={sentinelRef} aria-hidden className="h-px" />
            {isFetchingNextPage && (
              <li className="m-0 px-3.5 py-3 text-[12px] text-base-content/50">
                Загрузка…
              </li>
            )}
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
