import {
  httpDelete,
  httpGet,
  httpPut,
  settingsEndpoints
} from "@/shared/api";

import type {
  TaskTrackerBoards,
  TaskTrackerConnection,
  TaskTrackerConnections,
  UpdateTaskTrackerBoardsRequest,
  UpdateTaskTrackerConnectionRequest
} from "../model/types";

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

export function fetchTaskTrackerBoards(
  tracker: string,
  signal?: AbortSignal
): Promise<TaskTrackerBoards> {
  return httpGet<TaskTrackerBoards>(
    settingsEndpoints.getTaskTrackerBoards(tracker),
    signal
  );
}

export function setTaskTrackerBoards(
  tracker: string,
  request: UpdateTaskTrackerBoardsRequest,
  signal?: AbortSignal
): Promise<TaskTrackerBoards> {
  return httpPut<TaskTrackerBoards>(
    settingsEndpoints.setTaskTrackerBoards(tracker),
    request,
    signal
  );
}
