import {
  httpDelete,
  httpGet,
  httpPost,
  repositoriesEndpoints
} from "@/shared/api";

import type {
  BindRepositoryRequest,
  GitRepositoryRef,
  RepositoryBinding,
  RepositorySearchScope
} from "../model/types";

export interface SearchGithubRepositoriesParams {
  q?: string;
  scope?: RepositorySearchScope;
  limit?: number;
}

function buildSearchQuery(params: SearchGithubRepositoriesParams): string {
  const search = new URLSearchParams();
  if (params.q !== undefined && params.q.length > 0) search.set("q", params.q);
  if (params.scope !== undefined) search.set("scope", params.scope);
  if (params.limit !== undefined) search.set("limit", String(params.limit));
  const query = search.toString();
  return query.length > 0 ? `?${query}` : "";
}

export function searchGithubRepositories(
  params: SearchGithubRepositoriesParams = {},
  signal?: AbortSignal
): Promise<GitRepositoryRef[]> {
  const path = `${repositoriesEndpoints.searchGithubRepositories()}${buildSearchQuery(
    params
  )}`;
  return httpGet<GitRepositoryRef[]>(path, signal);
}

export function listMyGithubRepositories(
  limit?: number,
  signal?: AbortSignal
): Promise<GitRepositoryRef[]> {
  const suffix =
    limit !== undefined ? `?limit=${encodeURIComponent(String(limit))}` : "";
  return httpGet<GitRepositoryRef[]>(
    `${repositoriesEndpoints.listMyGithubRepositories()}${suffix}`,
    signal
  );
}

export function listIntentRepositories(
  intentId: string,
  signal?: AbortSignal
): Promise<RepositoryBinding[]> {
  return httpGet<RepositoryBinding[]>(
    repositoriesEndpoints.listIntentRepositories(intentId),
    signal
  );
}

export function bindIntentRepository(
  intentId: string,
  body: BindRepositoryRequest,
  signal?: AbortSignal
): Promise<RepositoryBinding> {
  return httpPost<RepositoryBinding>(
    repositoriesEndpoints.bindIntentRepository(intentId),
    body,
    signal
  );
}

export function unbindIntentRepository(
  intentId: string,
  bindingId: string,
  signal?: AbortSignal
): Promise<void> {
  return httpDelete(
    repositoriesEndpoints.unbindIntentRepository(intentId, bindingId),
    signal
  );
}
