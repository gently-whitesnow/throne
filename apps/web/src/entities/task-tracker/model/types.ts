import type { SettingsComponents } from "@/shared/api";

export type TaskTrackerConnections =
  SettingsComponents["schemas"]["TaskTrackerConnectionsDto"];

export type TaskTrackerConnection =
  SettingsComponents["schemas"]["TaskTrackerConnectionDto"];

export type TaskTrackerConnectionState =
  SettingsComponents["schemas"]["TaskTrackerConnectionState"];

export type TaskTrackerBoards =
  SettingsComponents["schemas"]["TaskTrackerBoardsDto"];

export type TaskTrackerSpace =
  SettingsComponents["schemas"]["TaskTrackerSpaceDto"];

export type TaskTrackerBoard =
  SettingsComponents["schemas"]["TaskTrackerBoardDto"];

export type TaskTrackerContextField =
  SettingsComponents["schemas"]["TaskTrackerContextField"];

export type TaskTrackerBoardSelectionEntry =
  SettingsComponents["schemas"]["TaskTrackerBoardSelectionEntry"];

export type UpdateTaskTrackerConnectionRequest =
  SettingsComponents["schemas"]["UpdateTaskTrackerConnectionRequest"];

export type UpdateTaskTrackerBoardsRequest =
  SettingsComponents["schemas"]["UpdateTaskTrackerBoardsRequest"];

export interface TaskTrackerStateMeta {
  label: string;
  className: string;
}

/**
 * Light-first semantic tokens for the connection-state pill on `/settings`.
 * `connected` → success, `invalid` → error (token rejected, not persisted),
 * `unreachable` → warning (upstream down), `not_configured` → neutral.
 */
export const taskTrackerStateMeta: Record<
  TaskTrackerConnectionState,
  TaskTrackerStateMeta
> = {
  connected: {
    label: "Подключено",
    className: "bg-success/10 text-success"
  },
  invalid: {
    label: "Токен отклонён",
    className: "bg-error/10 text-error"
  },
  unreachable: {
    label: "Недоступен",
    className: "bg-warning/20 text-warning"
  },
  not_configured: {
    label: "Не настроено",
    className: "bg-base-200 text-base-content/60"
  }
};

export interface TaskTrackerContextFieldOption {
  value: TaskTrackerContextField;
  label: string;
}

/** Human-readable labels for the per-board «context» field selector. */
export const taskTrackerContextFieldOptions: TaskTrackerContextFieldOption[] = [
  { value: "lane", label: "Дорожка (lane)" },
  { value: "tags", label: "Теги" },
  { value: "type", label: "Тип" },
  { value: "none", label: "Без контекста" }
];
