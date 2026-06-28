import { httpDelete, httpGet, httpPut, settingsEndpoints } from "@/shared/api";

import type {
  TaskTrackerBoardSearch,
  TaskTrackerBoardSelection,
  TaskTrackerConnection,
  TaskTrackerConnections,
  UpdateTaskTrackerBoardsRequest,
  UpdateTaskTrackerConnectionRequest
} from "../model/types";

export interface TaskTrackerBoardSearchParams {
  query?: string;
  skip?: number;
  take?: number;
  refresh?: boolean;
}

export function fetchTaskTrackerConnections(
  signal?: AbortSignal
): Promise<TaskTrackerConnections> {
  return httpGet<TaskTrackerConnections>(
    settingsEndpoints.getTaskTrackerConnections(),
    signal
  );
}

export function setTaskTrackerConnection(
  tracker: string,
  request: UpdateTaskTrackerConnectionRequest,
  signal?: AbortSignal
): Promise<TaskTrackerConnection> {
  return httpPut<TaskTrackerConnection>(
    settingsEndpoints.setTaskTrackerConnection(tracker),
    request,
    signal
  );
}

export function deleteTaskTrackerConnection(
  tracker: string,
  signal?: AbortSignal
): Promise<void> {
  return httpDelete(
    settingsEndpoints.deleteTaskTrackerConnection(tracker),
    signal
  );
}

export function searchTaskTrackerBoards(
  tracker: string,
  params: TaskTrackerBoardSearchParams,
  signal?: AbortSignal
): Promise<TaskTrackerBoardSearch> {
  const search = new URLSearchParams();
  if (params.query) search.set("query", params.query);
  if (params.skip !== undefined) search.set("skip", String(params.skip));
  if (params.take !== undefined) search.set("take", String(params.take));
  if (params.refresh) search.set("refresh", "true");
  const qs = search.toString();
  const base = settingsEndpoints.searchTaskTrackerBoards(tracker);
  return httpGet<TaskTrackerBoardSearch>(qs ? `${base}?${qs}` : base, signal);
}

export function fetchTaskTrackerBoardSelection(
  tracker: string,
  signal?: AbortSignal
): Promise<TaskTrackerBoardSelection> {
  return httpGet<TaskTrackerBoardSelection>(
    settingsEndpoints.getTaskTrackerBoardSelection(tracker),
    signal
  );
}

export function setTaskTrackerBoards(
  tracker: string,
  request: UpdateTaskTrackerBoardsRequest,
  signal?: AbortSignal
): Promise<TaskTrackerBoardSelection> {
  return httpPut<TaskTrackerBoardSelection>(
    settingsEndpoints.setTaskTrackerBoards(tracker),
    request,
    signal
  );
}
