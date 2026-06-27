import { AlertCircle, KanbanSquare } from "lucide-react";

import {
  taskTrackerStateMeta,
  useTaskTrackerConnectionsQuery,
  type TaskTrackerConnection
} from "@/entities/task-tracker";

import { TaskTrackerBoardsSelector } from "./TaskTrackerBoardsSelector";
import { TaskTrackerConnectionForm } from "./TaskTrackerConnectionForm";

/**
 * Settings → «Таск-трекеры».
 *
 * Перечисляет зарегистрированных провайдеров (Kaiten и др.): подключение по
 * base URL + API-токену и, для подключённых, выбор досок с per-board полем
 * «контекст». Boards-запрос монтируется только при `state === "connected"`.
 */
export function TaskTrackersCard() {
  const query = useTaskTrackerConnectionsQuery();
  const connections = query.data?.connections ?? [];

  return (
    <section
      aria-label="Таск-трекеры"
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-100 p-5"
    >
      <header className="flex items-start gap-3">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary"
        >
          <KanbanSquare size={18} strokeWidth={2} />
        </span>
        <div className="flex flex-col gap-1">
          <h3 className="m-0 text-base font-bold leading-tight">
            Таск-трекеры
          </h3>
          <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
            Подключите Kaiten по base URL и API-токену, затем выберите доски и
            поле, из которого выводить «контекст» карточек.
          </p>
        </div>
      </header>

      <TrackersBody
        isLoading={query.isLoading}
        error={query.error}
        connections={connections}
      />
    </section>
  );
}

interface TrackersBodyProps {
  isLoading: boolean;
  error: Error | null;
  connections: TaskTrackerConnection[];
}

function TrackersBody({ isLoading, error, connections }: TrackersBodyProps) {
  if (error instanceof Error) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось получить список трекеров: {error.message}</span>
      </p>
    );
  }

  if (connections.length === 0 && isLoading) {
    return <p className="m-0 text-sm text-base-content/60">Загружаем…</p>;
  }

  if (connections.length === 0) {
    return (
      <p className="m-0 text-sm text-base-content/60">
        Нет доступных трекеров.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {connections.map((connection) => (
        <TrackerRow key={connection.tracker} connection={connection} />
      ))}
    </div>
  );
}

function TrackerRow({ connection }: { connection: TaskTrackerConnection }) {
  const meta = taskTrackerStateMeta[connection.state];
  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-200/40 p-3">
      <div className="flex items-center justify-between gap-2">
        <h4 className="m-0 text-sm font-semibold leading-tight">
          {connection.display_name}
        </h4>
        <span
          data-testid={`task-tracker-state-${connection.tracker}`}
          className={`inline-flex w-fit items-center rounded-full px-2.5 py-1 text-xs font-semibold ${meta.className}`}
        >
          {meta.label}
        </span>
      </div>

      <TaskTrackerConnectionForm connection={connection} />

      {connection.state === "connected" ? (
        <TaskTrackerBoardsSelector tracker={connection.tracker} />
      ) : null}
    </div>
  );
}
