import {
  useInfiniteQuery,
  useQuery,
  type UseInfiniteQueryResult,
  type UseQueryResult
} from "@tanstack/react-query";
import { useEffect, useMemo } from "react";

import {
  httpGet,
  intentsEndpoints,
  type IntentsComponents
} from "@/shared/api";

import type {
  IntentAttachment,
  IntentDetail,
  IntentListItem,
  IntentStatus
} from "../model/types";

type IntentListPage = IntentsComponents["schemas"]["IntentListPageDto"];
export type IntentListSort = IntentsComponents["schemas"]["IntentListSort"];
export type IntentContextCounts =
  IntentsComponents["schemas"]["IntentContextCountsDto"];

export interface IntentListParams {
  status?: readonly IntentStatus[];
  tag?: string;
  untagged?: boolean;
  pinned?: boolean;
  query?: string;
  sort?: IntentListSort;
  limit?: number;
}

export const intentsQueryKeys = {
  all: ["intents"] as const,
  lists: () => [...intentsQueryKeys.all, "list"] as const,
  list: (params: IntentListParams = {}) =>
    [...intentsQueryKeys.lists(), normalizeParams(params)] as const,
  details: () => [...intentsQueryKeys.all, "detail"] as const,
  detail: (id: string) => [...intentsQueryKeys.details(), id] as const,
  attachments: (id: string) =>
    [...intentsQueryKeys.detail(id), "attachments"] as const
};

export const intentContextsQueryKeys = {
  all: ["intent-contexts"] as const
};

function normalizeParams(params: IntentListParams): IntentListParams {
  // Stable key shape — sorted statuses, undefined-stripped — so cache hits don't
  // depend on caller's prop ordering / whitespace.
  const status =
    params.status && params.status.length > 0
      ? [...params.status].sort()
      : undefined;
  const trimmedTag = params.tag?.trim();
  const trimmedQuery = params.query?.trim();
  return {
    status,
    tag:
      trimmedTag !== undefined && trimmedTag.length > 0
        ? trimmedTag
        : undefined,
    untagged: params.untagged ? true : undefined,
    pinned: params.pinned ? true : undefined,
    query:
      trimmedQuery !== undefined && trimmedQuery.length > 0
        ? trimmedQuery
        : undefined,
    sort: params.sort,
    limit: params.limit
  };
}

function buildListUrl(
  params: IntentListParams,
  cursor: string | undefined
): string {
  const qs = new URLSearchParams();
  if (cursor) qs.set("cursor", cursor);
  if (params.limit !== undefined) qs.set("limit", String(params.limit));
  for (const s of params.status ?? []) qs.append("status", s);
  if (params.tag) qs.set("tag", params.tag);
  if (params.untagged) qs.set("untagged", "true");
  if (params.pinned) qs.set("pinned", "true");
  if (params.query) qs.set("query", params.query);
  if (params.sort) qs.set("sort", params.sort);
  const base = intentsEndpoints.listIntents();
  const suffix = qs.toString();
  return suffix.length > 0 ? `${base}?${suffix}` : base;
}

/**
 * Курсорно-пагинированный запрос. Возвращает сырой результат `useInfiniteQuery` —
 * вызывающие сами решают, когда `fetchNextPage`. Виджеты с виртуализацией
 * (board) дергают подгрузку на скролл-сентинел.
 */
export function useInfiniteIntents(
  params: IntentListParams = {}
): UseInfiniteQueryResult<{
  pages: IntentListPage[];
  pageParams: (string | undefined)[];
}> {
  const normalized = useMemo(() => normalizeParams(params), [params]);
  return useInfiniteQuery({
    queryKey: intentsQueryKeys.list(normalized),
    queryFn: ({ pageParam, signal }) =>
      httpGet<IntentListPage>(buildListUrl(normalized, pageParam), signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.next_cursor ?? undefined
  });
}

/**
 * Backwards-compatible facade поверх курсорной пагинации: подсасывает все
 * страницы и отдаёт плоский `IntentListItem[]`. Подходит для виджетов, которым
 * нужна полная картина (контекст-рейл, дерево). На 100k+ нужно переезжать на
 * специализированные endpoint'ы — см. родительский интент.
 */
export interface UseFlatIntentsResult {
  data: IntentListItem[] | undefined;
  isPending: boolean;
  isError: boolean;
  isSuccess: boolean;
  isFetching: boolean;
  error: unknown;
}

export function useIntents(
  params: IntentListParams = {}
): UseFlatIntentsResult {
  const query = useInfiniteIntents(params);
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = query;

  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      void fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, query.data]);

  const items = useMemo(
    () => query.data?.pages.flatMap((p) => p.items),
    [query.data]
  );

  return {
    data: items,
    isPending: query.isPending,
    isError: query.isError,
    error: query.error,
    isFetching: query.isFetching || isFetchingNextPage,
    // Все страницы стянуты — иначе данные неполные.
    isSuccess: query.isSuccess && !hasNextPage
  };
}

/**
 * Агрегаты по контекстам одним запросом (`GET /api/v1/intents/contexts`).
 * Считает счётчики бакетов на сервере — рейл больше не вычитывает весь список
 * через `useIntents`, чтобы их посчитать. Инвалидируется realtime'ом через
 * `RealtimeQueryBridge` при создании/удалении/смене статуса/тегов/пина.
 */
export function useIntentContexts(): UseQueryResult<IntentContextCounts> {
  return useQuery({
    queryKey: intentContextsQueryKeys.all,
    queryFn: ({ signal }) =>
      httpGet<IntentContextCounts>(intentsEndpoints.getIntentContexts(), signal)
  });
}

export function useIntent(id: string | null): UseQueryResult<IntentDetail> {
  return useQuery({
    queryKey: id ? intentsQueryKeys.detail(id) : intentsQueryKeys.details(),
    queryFn: ({ signal }) => {
      if (!id) throw new Error("useIntent: id is required");
      return httpGet<IntentDetail>(intentsEndpoints.getIntent(id), signal);
    },
    enabled: id !== null
  });
}

export function useIntentAttachments(
  id: string | null
): UseQueryResult<IntentAttachment[]> {
  return useQuery({
    queryKey: id
      ? intentsQueryKeys.attachments(id)
      : intentsQueryKeys.details(),
    queryFn: ({ signal }) => {
      if (!id) throw new Error("useIntentAttachments: id is required");
      return httpGet<IntentAttachment[]>(
        intentsEndpoints.listIntentAttachments(id),
        signal
      );
    },
    enabled: id !== null
  });
}
