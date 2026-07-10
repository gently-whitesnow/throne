import { useEffect, useId, useRef, useState } from "react";

import {
  useBoardCardSearchQuery,
  type TaskTrackerCard
} from "@/entities/task-tracker-card";
import { useDebouncedValue } from "@/shared/lib";

interface CardSearchComboboxProps {
  tracker: string;
  boardId: string;
  value: string;
  onValueChange: (value: string) => void;
  onSelect: (card: TaskTrackerCard) => void;
  labelId: string;
  disabled?: boolean;
}

const CLOSE_DELAY_MS = 120;
const DEBOUNCE_MS = 250;

/**
 * Combobox для «Приложить карточку»: пустой фокус даёт топ-N по updated_at,
 * ввод — server-side поиск в рамках выбранной доски. Ввод целого числа
 * оставляем как fallback: пользователь, у которого уже есть id, отправляет
 * форму без выбора из списка.
 */
export function CardSearchCombobox({
  tracker,
  boardId,
  value,
  onValueChange,
  onSelect,
  labelId,
  disabled = false
}: CardSearchComboboxProps) {
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const closeTimer = useRef<number | null>(null);
  const looksLikeCardId = /^\d+$/.test(value.trim());
  const searchTerm = looksLikeCardId ? "" : value.trim();
  const debounced = useDebouncedValue(searchTerm, DEBOUNCE_MS);
  const search = useBoardCardSearchQuery(tracker, boardId, debounced, {
    enabled: open && !disabled && !looksLikeCardId && boardId.length > 0
  });
  const results = search.data ?? [];

  useEffect(
    () => () => {
      if (closeTimer.current !== null) {
        window.clearTimeout(closeTimer.current);
      }
    },
    []
  );

  const cancelClose = () => {
    if (closeTimer.current !== null) {
      window.clearTimeout(closeTimer.current);
      closeTimer.current = null;
    }
  };

  const scheduleClose = () => {
    cancelClose();
    closeTimer.current = window.setTimeout(() => {
      setOpen(false);
      closeTimer.current = null;
    }, CLOSE_DELAY_MS);
  };

  return (
    <div className="relative">
      <input
        type="text"
        role="combobox"
        aria-controls={listboxId}
        aria-expanded={open}
        aria-labelledby={labelId}
        data-testid="attach-card-search-input"
        className="input input-bordered input-sm w-full font-mono"
        placeholder="имя или id карточки"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onValueChange(event.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          cancelClose();
          setOpen(true);
        }}
        onBlur={scheduleClose}
      />

      {open ? (
        <ul
          id={listboxId}
          role="listbox"
          className="absolute z-[60] mt-1 max-h-80 w-full overflow-auto rounded-md border border-base-300 bg-base-100 p-1 shadow-lg"
        >
          <SearchResultItems
            isLoading={search.isLoading || search.isFetching}
            error={search.error}
            results={results}
            hasQuery={searchTerm.length > 0}
            looksLikeCardId={looksLikeCardId}
            currentValue={value.trim()}
            onSelect={(card) => {
              cancelClose();
              setOpen(false);
              onSelect(card);
            }}
          />
        </ul>
      ) : null}
    </div>
  );
}

interface SearchResultItemsProps {
  isLoading: boolean;
  error: unknown;
  results: TaskTrackerCard[];
  hasQuery: boolean;
  looksLikeCardId: boolean;
  currentValue: string;
  onSelect: (card: TaskTrackerCard) => void;
}

function SearchResultItems({
  isLoading,
  error,
  results,
  hasQuery,
  looksLikeCardId,
  currentValue,
  onSelect
}: SearchResultItemsProps) {
  if (looksLikeCardId) {
    return (
      <li className="px-3 py-2 text-xs text-base-content/60">
        Приложить карточку по id
        {currentValue.length > 0 ? ` #${currentValue}` : ""}.
      </li>
    );
  }

  if (error instanceof Error) {
    return (
      <li className="px-3 py-2 text-xs text-error" role="alert">
        Не удалось найти карточки: {error.message}
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
        {hasQuery ? "Ничего не нашли." : "На доске нет активных карточек."}
      </li>
    );
  }

  return (
    <>
      {results.map((card) => (
        <li
          key={card.card_id}
          role="option"
          aria-selected={false}
          data-testid={`attach-card-option-${card.card_id}`}
        >
          <button
            type="button"
            className="flex w-full flex-col gap-0.5 rounded px-2.5 py-1.5 text-left text-sm hover:bg-base-200"
            onMouseDown={(event) => {
              event.preventDefault();
              onSelect(card);
            }}
          >
            <span className="truncate">
              <span className="font-mono text-xs text-base-content/60">
                #{card.card_id}
              </span>{" "}
              · {card.title}
            </span>
            {card.column_title ? (
              <span className="text-xs text-base-content/50">
                {card.column_title}
              </span>
            ) : null}
          </button>
        </li>
      ))}
    </>
  );
}
