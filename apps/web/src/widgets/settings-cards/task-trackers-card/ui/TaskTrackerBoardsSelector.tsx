import { AlertCircle, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  taskTrackerContextFieldOptions,
  useSetTaskTrackerBoards,
  useTaskTrackerBoardSelectionQuery,
  type TaskTrackerBoardMatch,
  type TaskTrackerBoardSelectionEntry,
  type TaskTrackerContextField
} from "@/entities/task-tracker";
import { Button } from "@/shared/ui";

import { BoardSearchCombobox } from "./BoardSearchCombobox";

interface TaskTrackerBoardsSelectorProps {
  tracker: string;
}

type SelectionState = Map<string, TaskTrackerBoardSelectionEntry>;

/**
 * Выбор досок для подключённого трекера через поиск/автокомплит. Выбранные
 * доски показываются чипами с per-board полем «контекст»; добавление — только
 * через поиск по имени (одна дешёвая загрузка топологии на бэке, не плоский
 * список всех досок). Полный выбранный набор шлётся в PUT при «Сохранить».
 */
export function TaskTrackerBoardsSelector({
  tracker
}: TaskTrackerBoardsSelectorProps) {
  const selectionQuery = useTaskTrackerBoardSelectionQuery(tracker, true);
  const saveBoards = useSetTaskTrackerBoards();
  const [selection, setSelection] = useState<SelectionState>(() => new Map());

  const saved = selectionQuery.data;

  useEffect(() => {
    if (!saved) return;
    setSelection(buildSelection(saved.boards));
  }, [saved]);

  const selectedIds = useMemo(() => new Set(selection.keys()), [selection]);

  if (selectionQuery.isLoading) {
    return <p className="m-0 text-sm text-base-content/60">Загружаем доски…</p>;
  }

  if (selectionQuery.error instanceof Error) {
    return (
      <p
        role="alert"
        data-testid={`task-tracker-boards-error-${tracker}`}
        className="m-0 flex items-start gap-1.5 text-xs text-error"
      >
        <AlertCircle aria-hidden size={14} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить доски: {selectionQuery.error.message}</span>
      </p>
    );
  }

  const addBoard = (board: TaskTrackerBoardMatch) => {
    setSelection((prev) => {
      if (prev.has(board.board_id)) return prev;
      const next = new Map(prev);
      next.set(board.board_id, {
        space_id: board.space_id,
        space_title: board.space_title,
        board_id: board.board_id,
        board_title: board.board_title,
        context_field: "none"
      });
      return next;
    });
  };

  const removeBoard = (boardId: string) => {
    setSelection((prev) => {
      const next = new Map(prev);
      next.delete(boardId);
      return next;
    });
  };

  const setContext = (
    boardId: string,
    contextField: TaskTrackerContextField
  ) => {
    setSelection((prev) => {
      const existing = prev.get(boardId);
      if (!existing) return prev;
      const next = new Map(prev);
      next.set(boardId, { ...existing, context_field: contextField });
      return next;
    });
  };

  const handleSave = () => {
    saveBoards.mutate({
      tracker,
      request: { boards: [...selection.values()] }
    });
  };

  const chips = [...selection.values()];

  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-100 p-3">
      <div className="flex items-center justify-between gap-2">
        <h5 className="m-0 text-sm font-semibold leading-tight">Доски</h5>
        <Button
          aria-label="Сохранить выбор досок"
          data-testid={`task-tracker-boards-save-${tracker}`}
          variant="primary"
          disabled={saveBoards.isPending}
          onClick={handleSave}
        >
          {saveBoards.isPending ? "Сохраняем…" : "Сохранить доски"}
        </Button>
      </div>

      <BoardSearchCombobox
        tracker={tracker}
        selectedIds={selectedIds}
        onAdd={addBoard}
      />

      {chips.length === 0 ? (
        <p className="m-0 text-xs text-base-content/60">
          Доски не выбраны. Найдите доску по имени и добавьте её.
        </p>
      ) : (
        <ul className="m-0 flex flex-col gap-2 p-0">
          {chips.map((chip) => (
            <li
              key={chip.board_id}
              data-testid={`task-tracker-board-chip-${tracker}-${chip.board_id}`}
              className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-base-300 bg-base-200/40 px-3 py-2"
            >
              <span className="flex min-w-0 flex-col">
                <span className="truncate text-sm">{chip.board_title}</span>
                <span className="truncate text-xs text-base-content/50">
                  {chip.space_title}
                </span>
              </span>
              <div className="flex items-center gap-1.5">
                <label
                  htmlFor={`task-tracker-context-${tracker}-${chip.board_id}`}
                  className="text-xs text-base-content/60"
                >
                  Контекст
                </label>
                <select
                  id={`task-tracker-context-${tracker}-${chip.board_id}`}
                  data-testid={`task-tracker-context-${tracker}-${chip.board_id}`}
                  className="select select-sm select-bordered text-xs"
                  value={chip.context_field}
                  onChange={(event) => {
                    setContext(
                      chip.board_id,
                      event.target.value as TaskTrackerContextField
                    );
                  }}
                >
                  {taskTrackerContextFieldOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  aria-label={`Убрать доску ${chip.board_title ?? chip.board_id}`}
                  data-testid={`task-tracker-board-remove-${tracker}-${chip.board_id}`}
                  className="btn btn-ghost btn-xs"
                  onClick={() => {
                    removeBoard(chip.board_id);
                  }}
                >
                  <X aria-hidden size={14} strokeWidth={2} />
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {saveBoards.error instanceof Error ? (
        <p
          role="alert"
          data-testid={`task-tracker-boards-save-error-${tracker}`}
          className="m-0 text-xs text-error"
        >
          Не удалось сохранить доски: {saveBoards.error.message}
        </p>
      ) : null}
    </div>
  );
}

function buildSelection(
  boards: TaskTrackerBoardSelectionEntry[]
): SelectionState {
  const next: SelectionState = new Map();
  for (const board of boards) {
    next.set(board.board_id, board);
  }
  return next;
}
