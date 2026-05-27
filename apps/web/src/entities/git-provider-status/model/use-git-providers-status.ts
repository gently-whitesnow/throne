import { useCallback } from "react";

import { useGitProvidersStatusQuery } from "../api/git-providers-status-queries";
import type { GitProvidersStatus } from "./types";

export interface GitProvidersStatusState {
  status: GitProvidersStatus | null;
  isLoading: boolean;
  error: Error | null;
  refresh: () => void;
}

/**
 * Тонкая обёртка над react-query — публичный контракт сохраняется. Realtime для
 * auth-статуса не приходит, поэтому виджет дёргает `refresh` после `gh auth login`.
 *
 * `isLoading` маппится в `query.isLoading` (= первый fetch без данных), чтобы
 * кнопка «Проверить» не оставалась disabled на фоне refresh-фетча.
 */
export function useGitProvidersStatus(): GitProvidersStatusState {
  const query = useGitProvidersStatusQuery();
  const refresh = useCallback(() => {
    void query.refetch();
  }, [query]);

  return {
    status: query.data ?? null,
    // `isFetching && !data` — true только до первого ответа, не disables кнопку
    // на background-refetch'ах от `refresh()` / realtime инвалидаций.
    isLoading: query.isFetching && query.data === undefined,
    error: query.error,
    refresh
  };
}
