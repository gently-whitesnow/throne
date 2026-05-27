import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { WorkspaceSettings } from "../model/types";
import { isWorkspaceCalculating } from "../model/types";
import { fetchWorkspaceSettings } from "./workspace-settings-api";

const CALCULATING_POLL_MS = 2_000;

export const workspaceSettingsQueryKeys = {
  all: ["workspace-settings"] as const,
  current: () => [...workspaceSettingsQueryKeys.all, "current"] as const
};

export function useWorkspaceSettingsQuery(): UseQueryResult<WorkspaceSettings> {
  return useQuery({
    queryKey: workspaceSettingsQueryKeys.current(),
    queryFn: ({ signal }) => fetchWorkspaceSettings(signal),
    // Размер считается лениво на бэке: пока сервер отдаёт `calculating`,
    // мягко поллим до тех пор, пока статус не устаканится. Realtime-события
    // на этот ресурс не приходят, поэтому без поллинга UI зависнет.
    refetchInterval: (query) => {
      const data = query.state.data;
      if (data && isWorkspaceCalculating(data.status)) {
        return CALCULATING_POLL_MS;
      }
      return false;
    }
  });
}
