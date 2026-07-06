import type { SettingsComponents } from "@/shared/api";

export type TaskTrackerConnections =
  SettingsComponents["schemas"]["TaskTrackerConnectionsDto"];

export type TaskTrackerConnection =
  SettingsComponents["schemas"]["TaskTrackerConnectionDto"];

export type TaskTrackerConnectionState =
  SettingsComponents["schemas"]["TaskTrackerConnectionState"];

export type TaskTrackerBoardSearch =
  SettingsComponents["schemas"]["TaskTrackerBoardSearchDto"];

export type TaskTrackerBoardMatch =
  SettingsComponents["schemas"]["TaskTrackerBoardMatchDto"];

export type TaskTrackerBoardSelection =
  SettingsComponents["schemas"]["TaskTrackerBoardSelectionDto"];

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
 * `connected` → success, `auth` → error (token rejected, reconnect),
 * `offline` → warning (host unreachable, binding kept), `blocked` → error
 * (tariff plan), `not_configured` → neutral.
 */
export const taskTrackerStateMeta: Record<
  TaskTrackerConnectionState,
  TaskTrackerStateMeta
> = {
  connected: {
    label: "Подключено",
    className: "bg-success/10 text-success"
  },
  auth: {
    label: "Переподключите",
    className: "bg-error/10 text-error"
  },
  offline: {
    label: "Вне сети",
    className: "bg-warning/20 text-warning"
  },
  blocked: {
    label: "Заблокировано тарифом",
    className: "bg-error/10 text-error"
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
