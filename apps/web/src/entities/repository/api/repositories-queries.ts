import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { HttpError } from "@/shared/api";

import type {
  Repository,
  RepositoryCoordinate,
  RepositoryDocument,
  RepositoryDocumentVersion
} from "../model/types";
import {
  getRepository,
  getRepositoryDocument,
  listRepositories,
  listRepositoryDocumentVersions
} from "./repositories-api";

const coordinateKey = (c: RepositoryCoordinate) =>
  [c.provider, c.owner, c.repo] as const;

export const repositoriesQueryKeys = {
  all: ["repositories"] as const,
  list: () => [...repositoriesQueryKeys.all, "list"] as const,
  detail: (c: RepositoryCoordinate) =>
    [...repositoriesQueryKeys.all, "detail", ...coordinateKey(c)] as const,
  document: (c: RepositoryCoordinate, slug: string) =>
    [
      ...repositoriesQueryKeys.all,
      "document",
      ...coordinateKey(c),
      slug
    ] as const,
  documentVersions: (c: RepositoryCoordinate, slug: string) =>
    [
      ...repositoriesQueryKeys.all,
      "document-versions",
      ...coordinateKey(c),
      slug
    ] as const
};

/** A missing page (404) is a normal empty state, not a transient failure. */
function notFound(error: unknown): boolean {
  return error instanceof HttpError && error.status === 404;
}

export function useRepositoriesQuery(): UseQueryResult<Repository[]> {
  return useQuery({
    queryKey: repositoriesQueryKeys.list(),
    queryFn: ({ signal }) => listRepositories(signal)
  });
}

export function useRepositoryQuery(
  coordinate: RepositoryCoordinate | null
): UseQueryResult<Repository> {
  return useQuery({
    queryKey:
      coordinate !== null
        ? repositoriesQueryKeys.detail(coordinate)
        : repositoriesQueryKeys.all,
    queryFn: ({ signal }) => {
      if (coordinate === null) {
        throw new Error("useRepositoryQuery: coordinate is required");
      }
      return getRepository(coordinate, signal);
    },
    enabled: coordinate !== null
  });
}

export function useRepositoryDocumentQuery(
  coordinate: RepositoryCoordinate | null,
  slug: string
): UseQueryResult<RepositoryDocument> {
  return useQuery({
    queryKey:
      coordinate !== null
        ? repositoriesQueryKeys.document(coordinate, slug)
        : repositoriesQueryKeys.all,
    queryFn: ({ signal }) => {
      if (coordinate === null) {
        throw new Error("useRepositoryDocumentQuery: coordinate is required");
      }
      return getRepositoryDocument(coordinate, slug, signal);
    },
    enabled: coordinate !== null,
    retry: (count, error) => !notFound(error) && count < 2
  });
}

export function useRepositoryDocumentVersionsQuery(
  coordinate: RepositoryCoordinate | null,
  slug: string,
  enabled: boolean
): UseQueryResult<RepositoryDocumentVersion[]> {
  return useQuery({
    queryKey:
      coordinate !== null
        ? repositoriesQueryKeys.documentVersions(coordinate, slug)
        : repositoriesQueryKeys.all,
    queryFn: ({ signal }) => {
      if (coordinate === null) {
        throw new Error(
          "useRepositoryDocumentVersionsQuery: coordinate is required"
        );
      }
      return listRepositoryDocumentVersions(coordinate, slug, signal);
    },
    enabled: enabled && coordinate !== null,
    retry: (count, error) => !notFound(error) && count < 2
  });
}
