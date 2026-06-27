import { AlertCircle } from "lucide-react";
import { useEffect, useState } from "react";

import {
  taskTrackerContextFieldOptions,
  useSetTaskTrackerBoards,
  useTaskTrackerBoardsQuery,
  type TaskTrackerBoards,
  type TaskTrackerBoardSelectionEntry,
  type TaskTrackerContextField
} from "@/entities/task-tracker";
import { Button } from "@/shared/ui";

interface TaskTrackerBoardsSelectorProps {
  tracker: string;
}

interface BoardChoice {
  selected: boolean;
  contextField: TaskTrackerContextField;
}

type SelectionState = Map<string, BoardChoice>;

const boardKey = (spaceId: string, boardId: string) => `${spaceId}:${boardId}`;

/**
 * Выбор досок для подключённого трекера: чекбокс выбора + per-board селект
 * поля «контекст». Локальный выбор инициализируется из ответа GET и шлётся
 * целиком (полный выбранный набор) в PUT при «Сохранить доски».
 */
export function TaskTrackerBoardsSelector({
  tracker
}: TaskTrackerBoardsSelectorProps) {
  const boardsQuery = useTaskTrackerBoardsQuery(tracker, true);
  const saveBoards = useSetTaskTrackerBoards();
  const [selection, setSelection] = useState<SelectionState>(() => new Map());

  const data = boardsQuery.data;

  useEffect(() => {
    if (!data) return;
    setSelection(buildSelection(data));
  }, [data]);

  if (boardsQuery.isLoading) {
    return <p className="m-0 text-sm text-base-content/60">Загружаем доски…</p>;
  }

  if (boardsQuery.error instanceof Error) {
    return (
      <p
        role="alert"
        data-testid={`task-tracker-boards-error-${tracker}`}
        className="m-0 flex items-start gap-1.5 text-xs text-error"
      >
        <AlertCircle aria-hidden size={14} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить доски: {boardsQuery.error.message}</span>
      </p>
    );
  }

  const spaces = data?.spaces ?? [];
  if (spaces.length === 0) {
    return (
      <p className="m-0 text-sm text-base-content/60">
        Трекер не вернул ни одной доски.
      </p>
    );
  }

  const updateChoice = (key: string, patch: Partial<BoardChoice>) => {
    setSelection((prev) => {
      const next = new Map(prev);
      const existing = next.get(key);
      next.set(key, {
        selected: existing?.selected ?? false,
        contextField: existing?.contextField ?? "none",
        ...patch
      });
      return next;
    });
  };

  const handleSave = () => {
    if (!data) return;
    saveBoards.mutate({ tracker, request: { boards: collect(data, selection) } });
  };

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

      {spaces.map((space) => (
        <fieldset
          key={space.space_id}
          className="flex flex-col gap-2 border-0 p-0 m-0"
        >
          <legend className="m-0 px-0 text-xs font-semibold text-base-content/60">
            {space.space_title}
          </legend>
          {space.boards.map((board) => {
            const key = boardKey(space.space_id, board.board_id);
            const choice = selection.get(key);
            const selected = choice?.selected ?? false;
            const contextField = choice?.contextField ?? "none";
            const contextId = `task-tracker-context-${tracker}-${key}`;
            return (
              <div
                key={key}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-base-300 bg-base-200/40 px-3 py-2"
              >
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    className="checkbox checkbox-sm"
                    data-testid={`task-tracker-board-${tracker}-${key}`}
                    checked={selected}
                    onChange={(event) => {
                      updateChoice(key, { selected: event.target.checked });
                    }}
                  />
                  {board.board_title}
                </label>
                <div className="flex items-center gap-1.5">
                  <label
                    htmlFor={contextId}
                    className="text-xs text-base-content/60"
                  >
                    Контекст
                  </label>
                  <select
                    id={contextId}
                    data-testid={contextId}
                    className="select select-sm select-bordered text-xs"
                    value={contextField}
                    disabled={!selected}
                    onChange={(event) => {
                      updateChoice(key, {
                        contextField: event.target
                          .value as TaskTrackerContextField
                      });
                    }}
                  >
                    {taskTrackerContextFieldOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            );
          })}
        </fieldset>
      ))}

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

function buildSelection(data: TaskTrackerBoards): SelectionState {
  const next: SelectionState = new Map();
  for (const space of data.spaces) {
    for (const board of space.boards) {
      next.set(boardKey(space.space_id, board.board_id), {
        selected: board.selected,
        contextField: board.context_field
      });
    }
  }
  return next;
}

function collect(
  data: TaskTrackerBoards,
  selection: SelectionState
): TaskTrackerBoardSelectionEntry[] {
  const entries: TaskTrackerBoardSelectionEntry[] = [];
  for (const space of data.spaces) {
    for (const board of space.boards) {
      const choice = selection.get(boardKey(space.space_id, board.board_id));
      if (!choice?.selected) continue;
      entries.push({
        space_id: space.space_id,
        space_title: space.space_title,
        board_id: board.board_id,
        board_title: board.board_title,
        context_field: choice.contextField
      });
    }
  }
  return entries;
}
