import {
  httpDelete,
  httpGet,
  httpPost,
  repositoriesEndpoints
} from "@/shared/api";

import type {
  BindRepositoryRequest,
  GitProvider,
  GitBranchRef,
  GitPullRequestRef,
  GitRepositoryRef,
  RepositoryBinding,
  RepositorySearchScope
} from "../model/types";

const DEFAULT_PROVIDER: GitProvider = "github";

function pathSegment(value: string): string {
  return encodeURIComponent(value);
}

export interface SearchGitProviderRepositoriesParams {
  provider?: GitProvider;
  q?: string;
  scope?: RepositorySearchScope;
  limit?: number;
}

function buildSearchQuery(params: SearchGitProviderRepositoriesParams): string {
  const search = new URLSearchParams();
  if (params.q !== undefined && params.q.length > 0) search.set("q", params.q);
  if (params.scope !== undefined) search.set("scope", params.scope);
  if (params.limit !== undefined) search.set("limit", String(params.limit));
  const query = search.toString();
  return query.length > 0 ? `?${query}` : "";
}

export function searchGitProviderRepositories(
  params: SearchGitProviderRepositoriesParams = {},
  signal?: AbortSignal
): Promise<GitRepositoryRef[]> {
  const provider = params.provider ?? DEFAULT_PROVIDER;
  const path = `${repositoriesEndpoints.searchGitProviderRepositories(
    provider
  )}${buildSearchQuery(params)}`;
  return httpGet<GitRepositoryRef[]>(path, signal);
}

export const searchGithubRepositories = searchGitProviderRepositories;
export type SearchGithubRepositoriesParams =
  SearchGitProviderRepositoriesParams;

export interface ListGitProviderRepositoryRefsParams {
  provider?: GitProvider;
  q?: string;
  limit?: number;
}

function buildRefsQuery(params: ListGitProviderRepositoryRefsParams): string {
  const search = new URLSearchParams();
  if (params.q !== undefined && params.q.length > 0) search.set("q", params.q);
  if (params.limit !== undefined) search.set("limit", String(params.limit));
  const query = search.toString();
  return query.length > 0 ? `?${query}` : "";
}

export function listGitProviderRepositoryBranches(
  owner: string,
  repo: string,
  params: ListGitProviderRepositoryRefsParams = {},
  signal?: AbortSignal
): Promise<GitBranchRef[]> {
  const provider = params.provider ?? DEFAULT_PROVIDER;
  const path = `${repositoriesEndpoints.listGitProviderRepositoryBranches(
    provider,
    pathSegment(owner),
    pathSegment(repo)
  )}${buildRefsQuery(params)}`;
  return httpGet<GitBranchRef[]>(path, signal);
}

export const listGithubRepositoryBranches = listGitProviderRepositoryBranches;

export function listGitProviderRepositoryPullRequests(
  owner: string,
  repo: string,
  params: ListGitProviderRepositoryRefsParams = {},
  signal?: AbortSignal
): Promise<GitPullRequestRef[]> {
  const provider = params.provider ?? DEFAULT_PROVIDER;
  const path = `${repositoriesEndpoints.listGitProviderRepositoryPullRequests(
    provider,
    pathSegment(owner),
    pathSegment(repo)
  )}${buildRefsQuery(params)}`;
  return httpGet<GitPullRequestRef[]>(path, signal);
}

export const listGithubRepositoryPullRequests =
  listGitProviderRepositoryPullRequests;
export type ListGithubRepositoryRefsParams =
  ListGitProviderRepositoryRefsParams;

export function listGitProviderRepositories(
  provider: GitProvider = DEFAULT_PROVIDER,
  limit?: number,
  signal?: AbortSignal
): Promise<GitRepositoryRef[]> {
  const suffix =
    limit !== undefined ? `?limit=${encodeURIComponent(String(limit))}` : "";
  return httpGet<GitRepositoryRef[]>(
    `${repositoriesEndpoints.listGitProviderRepositories(provider)}${suffix}`,
    signal
  );
}

export function listMyGithubRepositories(
  limit?: number,
  signal?: AbortSignal
): Promise<GitRepositoryRef[]> {
  return listGitProviderRepositories(DEFAULT_PROVIDER, limit, signal);
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

export function attachIntentRepositoryPullRequest(
  intentId: string,
  bindingId: string,
  pullRequestNumber: number,
  signal?: AbortSignal
): Promise<RepositoryBinding> {
  return httpPost<RepositoryBinding>(
    repositoriesEndpoints.attachIntentRepositoryPullRequest(
      intentId,
      bindingId
    ),
    { pull_request_number: pullRequestNumber },
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
