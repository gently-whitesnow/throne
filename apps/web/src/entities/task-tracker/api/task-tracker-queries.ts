import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type {
  TaskTrackerBoards,
  TaskTrackerConnection,
  TaskTrackerConnections,
  UpdateTaskTrackerBoardsRequest,
  UpdateTaskTrackerConnectionRequest
} from "../model/types";
import {
  deleteTaskTrackerConnection,
  fetchTaskTrackerBoards,
  fetchTaskTrackerConnections,
  setTaskTrackerBoards,
  setTaskTrackerConnection
} from "./task-tracker-api";

export const taskTrackerQueryKeys = {
  all: ["task-trackers"] as const,
  connections: () => [...taskTrackerQueryKeys.all, "connections"] as const,
  boards: (tracker: string) =>
    [...taskTrackerQueryKeys.all, "boards", tracker] as const
};

export function useTaskTrackerConnectionsQuery(): UseQueryResult<TaskTrackerConnections> {
  return useQuery({
    queryKey: taskTrackerQueryKeys.connections(),
    queryFn: ({ signal }) => fetchTaskTrackerConnections(signal)
  });
}

export function useTaskTrackerBoardsQuery(
  tracker: string,
  enabled: boolean
): UseQueryResult<TaskTrackerBoards> {
  return useQuery({
    queryKey: taskTrackerQueryKeys.boards(tracker),
    queryFn: ({ signal }) => fetchTaskTrackerBoards(tracker, signal),
    enabled
  });
}

interface SetConnectionVariables {
  tracker: string;
  request: UpdateTaskTrackerConnectionRequest;
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
      void queryClient.invalidateQueries({
        queryKey: taskTrackerQueryKeys.connections()
      });
      void queryClient.invalidateQueries({
        queryKey: taskTrackerQueryKeys.boards(tracker)
      });
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
      void queryClient.invalidateQueries({
        queryKey: taskTrackerQueryKeys.connections()
      });
      void queryClient.invalidateQueries({
        queryKey: taskTrackerQueryKeys.boards(tracker)
      });
    }
  });
}

interface SetBoardsVariables {
  tracker: string;
  request: UpdateTaskTrackerBoardsRequest;
}

export function useSetTaskTrackerBoards(): UseMutationResult<
  TaskTrackerBoards,
  Error,
  SetBoardsVariables
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ tracker, request }: SetBoardsVariables) =>
      setTaskTrackerBoards(tracker, request),
    onSuccess: (data, { tracker }) => {
      queryClient.setQueryData(taskTrackerQueryKeys.boards(tracker), data);
    }
  });
}
