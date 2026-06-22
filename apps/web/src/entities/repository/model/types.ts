import type { RepositoriesComponents } from "@/shared/api";

export type RepositoryDocumentSummary =
  RepositoriesComponents["schemas"]["RepositoryDocumentSummaryDto"];

export type RepositoryDocument =
  RepositoriesComponents["schemas"]["RepositoryDocumentDto"];

export type PutRepositoryDocumentRequest =
  RepositoriesComponents["schemas"]["PutRepositoryDocumentRequest"];

export type RepositoryDocumentVersion =
  RepositoriesComponents["schemas"]["RepositoryDocumentVersionDto"];

export interface RepositoryCoordinate {
  provider: string;
  host?: string;
  owner: string;
  repo: string;
}
