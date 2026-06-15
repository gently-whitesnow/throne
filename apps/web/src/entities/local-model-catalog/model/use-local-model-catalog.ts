import { useCallback } from "react";

import { useLocalModelCatalogQuery } from "../api/local-model-catalog-queries";
import type { LocalModelCatalog } from "./types";

export interface LocalModelCatalogState {
  catalog: LocalModelCatalog | null;
  isLoading: boolean;
  error: Error | null;
  refresh: () => void;
}

/**
 * Тонкая обёртка над react-query. Discovery не приходит через realtime, поэтому
 * виджет дёргает `refresh` руками после поднятия локального endpoint.
 *
 * `isLoading` — `isFetching && !data` (только до первого ответа), чтобы кнопка
 * «Проверить» не оставалась disabled на background-refetch'ах.
 */
export function useLocalModelCatalog(): LocalModelCatalogState {
  const query = useLocalModelCatalogQuery();
  const refresh = useCallback(() => {
    void query.refetch();
  }, [query]);

  return {
    catalog: query.data ?? null,
    isLoading: query.isFetching && query.data === undefined,
    error: query.error,
    refresh
  };
}
