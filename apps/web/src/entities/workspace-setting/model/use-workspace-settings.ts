import { useCallback } from "react";

import { useWorkspaceSettingsQuery } from "../api/workspace-settings-queries";
import type { WorkspaceSettings } from "./types";

export interface WorkspaceSettingsState {
  settings: WorkspaceSettings | null;
  isLoading: boolean;
  error: Error | null;
  refresh: () => void;
}

/**
 * Тонкая обёртка над react-query — публичный контракт `{ settings, isLoading,
 * error, refresh }` сохраняется, чтобы виджеты не пришлось трогать. Поллинг при
 * `status=calculating` живёт внутри `useWorkspaceSettingsQuery` через
 * `refetchInterval`; realtime-события на этот ресурс не приходят.
 */
export function useWorkspaceSettings(): WorkspaceSettingsState {
  const query = useWorkspaceSettingsQuery();
  const refresh = useCallback(() => {
    void query.refetch();
  }, [query]);

  return {
    settings: query.data ?? null,
    isLoading: query.isLoading,
    error: query.error,
    refresh
  };
}
