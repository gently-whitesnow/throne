import { httpGet, httpPut, repositoriesEndpoints } from "@/shared/api";

import type {
  PutRepositoryDocumentRequest,
  RepositoryCoordinate,
  RepositoryDocument,
  RepositoryDocumentSummary,
  RepositoryDocumentVersion
} from "../model/types";

export function listRepositoryDocuments(
  { provider, owner, repo }: RepositoryCoordinate,
  signal?: AbortSignal
): Promise<RepositoryDocumentSummary[]> {
  return httpGet<RepositoryDocumentSummary[]>(
    repositoriesEndpoints.listRepositoryDocuments(provider, owner, repo),
    signal
  );
}

export function getRepositoryDocument(
  { provider, owner, repo }: RepositoryCoordinate,
  slug: string,
  signal?: AbortSignal
): Promise<RepositoryDocument> {
  return httpGet<RepositoryDocument>(
    repositoriesEndpoints.getRepositoryDocument(provider, owner, repo, slug),
    signal
  );
}

export function putRepositoryDocument(
  { provider, owner, repo }: RepositoryCoordinate,
  slug: string,
  body: PutRepositoryDocumentRequest,
  signal?: AbortSignal
): Promise<RepositoryDocument> {
  return httpPut<RepositoryDocument>(
    repositoriesEndpoints.putRepositoryDocument(provider, owner, repo, slug),
    body,
    signal
  );
}

export function listRepositoryDocumentVersions(
  { provider, owner, repo }: RepositoryCoordinate,
  slug: string,
  signal?: AbortSignal
): Promise<RepositoryDocumentVersion[]> {
  return httpGet<RepositoryDocumentVersion[]>(
    repositoriesEndpoints.listRepositoryDocumentVersions(
      provider,
      owner,
      repo,
      slug
    ),
    signal
  );
}
