import { Plus } from "lucide-react";

export interface TagPickerOption {
  name: string;
}

interface TagOptionsListProps {
  listboxId: string;
  query: string;
  candidates: readonly TagPickerOption[];
  selected: readonly string[];
  activeIndex: number;
  onHover: (index: number) => void;
  onPick: (name: string) => void;
  canCreate: boolean;
  createSlug: string;
  creating: boolean;
  onCreate: () => void;
}

export function TagOptionsList({
  listboxId,
  query,
  candidates,
  selected,
  activeIndex,
  onHover,
  onPick,
  canCreate,
  createSlug,
  creating,
  onCreate
}: TagOptionsListProps) {
  const empty = candidates.length === 0 && !canCreate;
  return (
    <ul
      id={listboxId}
      role="listbox"
      aria-label="Доступные теги"
      className="absolute left-0 right-0 top-full z-30 mt-1 flex max-h-64 flex-col overflow-y-auto rounded-md border border-base-300 bg-base-100 py-1 shadow-lg"
    >
      {empty ? (
        <li className="px-3 py-2 text-[12px] text-base-content/60">
          {query.trim().length > 0 ? "Нет совпадений." : "Тегов пока нет."}
        </li>
      ) : null}
      {candidates.map((option, idx) => {
        const isSelected = selected.includes(option.name);
        const active = idx === activeIndex;
        return (
          <li
            key={option.name}
            role="option"
            aria-selected={isSelected}
            className={[
              "flex cursor-pointer items-center justify-between gap-2 px-3 py-1.5 text-[13px]",
              active ? "bg-primary/10" : "hover:bg-base-200"
            ].join(" ")}
            onMouseEnter={() => {
              onHover(idx);
            }}
            onMouseDown={(event) => {
              event.preventDefault();
              onPick(option.name);
            }}
          >
            <span className="truncate text-base-content">#{option.name}</span>
            <span className="flex shrink-0 items-center gap-2">
              {isSelected ? (
                <span className="text-[10px] uppercase tracking-wider text-primary">
                  выбран
                </span>
              ) : null}
            </span>
          </li>
        );
      })}
      {canCreate ? (
        <li
          role="option"
          aria-selected={false}
          className={[
            "flex cursor-pointer items-center gap-2 border-t border-base-300 px-3 py-1.5 text-[13px]",
            activeIndex === candidates.length
              ? "bg-primary/10"
              : "hover:bg-base-200"
          ].join(" ")}
          onMouseEnter={() => {
            onHover(candidates.length);
          }}
          onMouseDown={(event) => {
            event.preventDefault();
            onCreate();
          }}
        >
          <Plus aria-hidden size={14} strokeWidth={2} />
          <span className="truncate">
            Создать тег{" "}
            <span className="font-mono text-primary">#{createSlug}</span>
          </span>
          {creating ? (
            <span className="ml-auto text-[10px] uppercase tracking-wider text-base-content/60">
              создаём…
            </span>
          ) : null}
        </li>
      ) : null}
    </ul>
  );
}
