import { useMemo } from "react";

import {
  compareSortKeys,
  useInfiniteIntents,
  type IntentListItem
} from "@/entities/intent";

import { matchesContext } from "./board-helpers";
import { contextToParams, needsClientPostFilter } from "./context-params";

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
 * Тянет страницы интентов под текущий контекст через `useInfiniteIntents`,
 * собирает плоский массив видимых элементов и применяет клиентский
 * post-filter в тех бакетах, где сервер не умеет фильтровать (untagged /
 * pinned / archive-untagged). Сортировка — bytewise `sort_key` для стабильного
 * порядка между страницами; сервер уже отдаёт страницы в этом же порядке.
 */
export function useBoardItems(context: string | null): BoardItemsResult {
  const params = useMemo(() => contextToParams(context), [context]);
  const query = useInfiniteIntents(params);
  const postFilter = needsClientPostFilter(context);

  const allItems = useMemo<readonly IntentListItem[]>(() => {
    if (!query.data) return EMPTY;
    return query.data.pages.flatMap((p) => p.items);
  }, [query.data]);

  const visibleItems = useMemo<readonly IntentListItem[]>(() => {
    const ordered = [...allItems].sort((a, b) =>
      compareSortKeys(a.sort_key, b.sort_key)
    );
    if (!postFilter) return ordered;
    return ordered.filter((i) => matchesContext(i, context));
  }, [allItems, context, postFilter]);

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
