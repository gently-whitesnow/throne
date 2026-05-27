import { useMemo } from "react";

import {
  compareSortKeys,
  contextToParams,
  useInfiniteIntents,
  type IntentListItem
} from "@/entities/intent";

interface BoardItemsResult {
  allItems: readonly IntentListItem[];
  visibleItems: readonly IntentListItem[];
  isPending: boolean;
  isSuccess: boolean;
  isError: boolean;
  error: unknown;
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  fetchNextPage: () => void;
}

const EMPTY: readonly IntentListItem[] = [];

/**
 * Тянет страницы интентов под текущий контекст через `useInfiniteIntents`.
 * Контекст целиком выражается серверными фильтрами (status / tag / untagged /
 * pinned), поэтому клиентского post-filter нет. Сортировка — bytewise
 * `sort_key` для стабильного порядка между страницами; сервер уже отдаёт
 * страницы в этом же порядке.
 */
export function useBoardItems(context: string | null): BoardItemsResult {
  const params = useMemo(() => contextToParams(context), [context]);
  const query = useInfiniteIntents(params);

  const allItems = useMemo<readonly IntentListItem[]>(() => {
    if (!query.data) return EMPTY;
    return query.data.pages.flatMap((p) => p.items);
  }, [query.data]);

  const visibleItems = useMemo<readonly IntentListItem[]>(
    () => [...allItems].sort((a, b) => compareSortKeys(a.sort_key, b.sort_key)),
    [allItems]
  );

  return {
    allItems,
    visibleItems,
    isPending: query.isPending,
    isSuccess: query.isSuccess,
    isError: query.isError,
    error: query.error,
    hasNextPage: query.hasNextPage,
    isFetchingNextPage: query.isFetchingNextPage,
    fetchNextPage: () => {
      void query.fetchNextPage();
    }
  };
}
