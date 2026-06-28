import { useQueryClient } from "@tanstack/react-query";
import { Check, RefreshCw, Search } from "lucide-react";
import { useState } from "react";

import {
  searchTaskTrackerBoards,
  taskTrackerQueryKeys,
  useTaskTrackerBoardSearchQuery,
  type TaskTrackerBoardMatch
} from "@/entities/task-tracker";
import { useDebouncedValue } from "@/shared/lib";

interface BoardSearchComboboxProps {
  tracker: string;
  selectedIds: Set<string>;
  onAdd: (board: TaskTrackerBoardMatch) => void;
}

/**
 * Поиск доски по имени с серверным автокомплитом: ввод дебаунсится, запрос
 * фильтрует доски в кеше бэкенда (не ходит в Kaiten на каждый символ). Уже
 * выбранные доски в списке помечены и недоступны для повторного добавления.
 * «Обновить» инвалидирует кеш топологии и перетягивает её заново.
 */
export function BoardSearchCombobox({
  tracker,
  selectedIds,
  onAdd
}: BoardSearchComboboxProps) {
  const queryClient = useQueryClient();
  const [input, setInput] = useState("");
  const [open, setOpen] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const query = useDebouncedValue(input.trim(), 250);

  const search = useTaskTrackerBoardSearchQuery(tracker, query, open);
  const results = search.data?.boards ?? [];

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await searchTaskTrackerBoards(tracker, {
        query: query || undefined,
        refresh: true
      });
      await queryClient.invalidateQueries({
        queryKey: [...taskTrackerQueryKeys.all, "board-search", tracker]
      });
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <div className="flex items-start gap-2">
      <div className="relative flex-1">
        <span className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-base-content/40">
          <Search aria-hidden size={14} strokeWidth={2} />
        </span>
        <input
          type="text"
          role="combobox"
          aria-expanded={open}
          aria-controls={`task-tracker-board-results-${tracker}`}
          data-testid={`task-tracker-board-search-${tracker}`}
          className="input input-sm input-bordered w-full pl-8 text-sm"
          placeholder="Поиск доски по имени…"
          value={input}
          onChange={(event) => {
            setInput(event.target.value);
          }}
          onFocus={() => {
            setOpen(true);
          }}
          onBlur={() => {
            window.setTimeout(() => {
              setOpen(false);
            }, 120);
          }}
        />

        {open ? (
          <ul
            id={`task-tracker-board-results-${tracker}`}
            role="listbox"
            className="absolute z-10 mt-1 max-h-64 w-full overflow-auto rounded-md border border-base-300 bg-base-100 p-1 shadow-lg"
          >
            <SearchResultItems
              tracker={tracker}
              isLoading={search.isLoading || search.isFetching}
              error={search.error}
              results={results}
              selectedIds={selectedIds}
              onAdd={onAdd}
            />
          </ul>
        ) : null}
      </div>

      <button
        type="button"
        aria-label="Обновить список досок"
        data-testid={`task-tracker-boards-refresh-${tracker}`}
        className="btn btn-sm btn-ghost"
        disabled={refreshing}
        onClick={() => {
          void handleRefresh();
        }}
      >
        <RefreshCw
          aria-hidden
          size={14}
          strokeWidth={2}
          className={refreshing ? "animate-spin" : undefined}
        />
      </button>
    </div>
  );
}

interface SearchResultItemsProps {
  tracker: string;
  isLoading: boolean;
  error: unknown;
  results: TaskTrackerBoardMatch[];
  selectedIds: Set<string>;
  onAdd: (board: TaskTrackerBoardMatch) => void;
}

function SearchResultItems({
  tracker,
  isLoading,
  error,
  results,
  selectedIds,
  onAdd
}: SearchResultItemsProps) {
  if (error instanceof Error) {
    return (
      <li className="px-3 py-2 text-xs text-error" role="alert">
        Не удалось загрузить доски: {error.message}
      </li>
    );
  }

  if (isLoading) {
    return (
      <li className="px-3 py-2 text-xs text-base-content/60">Загружаем…</li>
    );
  }

  if (results.length === 0) {
    return (
      <li className="px-3 py-2 text-xs text-base-content/60">
        Доски не найдены.
      </li>
    );
  }

  return (
    <>
      {results.map((board) => {
        const already = selectedIds.has(board.board_id);
        return (
          <li key={board.board_id} role="option" aria-selected={already}>
            <button
              type="button"
              data-testid={`task-tracker-board-option-${tracker}-${board.board_id}`}
              className="flex w-full items-center justify-between gap-2 rounded px-2.5 py-1.5 text-left text-sm hover:bg-base-200 disabled:opacity-50"
              disabled={already}
              onMouseDown={(event) => {
                event.preventDefault();
                onAdd(board);
              }}
            >
              <span className="flex flex-col">
                <span>{board.board_title}</span>
                <span className="text-xs text-base-content/50">
                  {board.space_title}
                </span>
              </span>
              {already ? (
                <Check
                  aria-hidden
                  size={14}
                  strokeWidth={2}
                  className="text-success"
                />
              ) : null}
            </button>
          </li>
        );
      })}
    </>
  );
}
