import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type {
  TaskTrackerBoardSearch,
  TaskTrackerBoardSelection,
  TaskTrackerBoardSync,
  TaskTrackerConnection,
  TaskTrackerConnections,
  UpdateTaskTrackerBoardsRequest,
  UpdateTaskTrackerConnectionRequest
} from "../model/types";
import {
  deleteTaskTrackerConnection,
  fetchTaskTrackerBoardSelection,
  fetchTaskTrackerConnections,
  forceRefreshTaskTrackerBoard,
  searchTaskTrackerBoards,
  setTaskTrackerBoards,
  setTaskTrackerConnection,
  type TaskTrackerBoardSearchParams
} from "./task-tracker-api";

export const taskTrackerQueryKeys = {
  all: ["task-trackers"] as const,
  connections: () => [...taskTrackerQueryKeys.all, "connections"] as const,
  boardSearch: (tracker: string, query: string) =>
    [...taskTrackerQueryKeys.all, "board-search", tracker, query] as const,
  boardSelection: (tracker: string) =>
    [...taskTrackerQueryKeys.all, "board-selection", tracker] as const
};

export function useTaskTrackerConnectionsQuery(): UseQueryResult<TaskTrackerConnections> {
  return useQuery({
    queryKey: taskTrackerQueryKeys.connections(),
    queryFn: ({ signal }) => fetchTaskTrackerConnections(signal)
  });
}

export function useTaskTrackerBoardSearchQuery(
  tracker: string,
  query: string,
  enabled: boolean
): UseQueryResult<TaskTrackerBoardSearch> {
  return useQuery({
    queryKey: taskTrackerQueryKeys.boardSearch(tracker, query),
    queryFn: ({ signal }) =>
      searchTaskTrackerBoards(tracker, { query: query || undefined }, signal),
    enabled
  });
}

export function useTaskTrackerBoardSelectionQuery(
  tracker: string,
  enabled: boolean
): UseQueryResult<TaskTrackerBoardSelection> {
  return useQuery({
    queryKey: taskTrackerQueryKeys.boardSelection(tracker),
    queryFn: ({ signal }) => fetchTaskTrackerBoardSelection(tracker, signal),
    enabled
  });
}

interface SetConnectionVariables {
  tracker: string;
  request: UpdateTaskTrackerConnectionRequest;
}

function invalidateTrackerBoards(
  queryClient: ReturnType<typeof useQueryClient>,
  tracker: string
) {
  void queryClient.invalidateQueries({
    queryKey: taskTrackerQueryKeys.connections()
  });
  void queryClient.invalidateQueries({
    queryKey: taskTrackerQueryKeys.boardSelection(tracker)
  });
  void queryClient.invalidateQueries({
    queryKey: [...taskTrackerQueryKeys.all, "board-search", tracker]
  });
}

export function useSetTaskTrackerConnection(): UseMutationResult<
  TaskTrackerConnection,
  Error,
  SetConnectionVariables
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ tracker, request }: SetConnectionVariables) =>
      setTaskTrackerConnection(tracker, request),
    onSuccess: (_data, { tracker }) => {
      invalidateTrackerBoards(queryClient, tracker);
    }
  });
}

export function useDeleteTaskTrackerConnection(): UseMutationResult<
  void,
  Error,
  string
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (tracker: string) => deleteTaskTrackerConnection(tracker),
    onSuccess: (_data, tracker) => {
      invalidateTrackerBoards(queryClient, tracker);
    }
  });
}

interface SetBoardsVariables {
  tracker: string;
  request: UpdateTaskTrackerBoardsRequest;
}

export function useSetTaskTrackerBoards(): UseMutationResult<
  TaskTrackerBoardSelection,
  Error,
  SetBoardsVariables
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ tracker, request }: SetBoardsVariables) =>
      setTaskTrackerBoards(tracker, request),
    onSuccess: (data, { tracker }) => {
      queryClient.setQueryData(
        taskTrackerQueryKeys.boardSelection(tracker),
        data
      );
    }
  });
}

interface ForceRefreshBoardVariables {
  tracker: string;
  board: string;
}

/**
 * Board-level «Обновить»: triggers the periodic board sync on demand. Invalidation of the rail
 * counts and the board's intent list is left to the caller — the refreshed mirror content also
 * lands over the realtime stream, so the caller just nudges the affected queries.
 */
export function useForceRefreshTaskTrackerBoard(): UseMutationResult<
  TaskTrackerBoardSync,
  Error,
  ForceRefreshBoardVariables
> {
  return useMutation({
    mutationFn: ({ tracker, board }: ForceRefreshBoardVariables) =>
      forceRefreshTaskTrackerBoard(tracker, board)
  });
}

export type { TaskTrackerBoardSearchParams };
