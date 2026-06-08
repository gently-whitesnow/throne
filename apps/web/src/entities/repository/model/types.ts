import type { RepositoriesComponents } from "@/shared/api";

export type Repository = RepositoriesComponents["schemas"]["RepositoryDto"];

export type CreateRepositoryRequest =
  RepositoriesComponents["schemas"]["CreateRepositoryRequest"];

export type RepositoryDocumentSummary =
  RepositoriesComponents["schemas"]["RepositoryDocumentSummaryDto"];

export type RepositoryDocument =
  RepositoriesComponents["schemas"]["RepositoryDocumentDto"];

export type PutRepositoryDocumentRequest =
  RepositoriesComponents["schemas"]["PutRepositoryDocumentRequest"];

export type RepositoryDocumentVersion =
  RepositoriesComponents["schemas"]["RepositoryDocumentVersionDto"];

export type RepositoryArtifactRenderHint =
  RepositoriesComponents["schemas"]["RepositoryArtifactRenderHint"];

export interface RepositoryCoordinate {
  provider: string;
  host?: string;
  owner: string;
  repo: string;
}

/** The one knowledge page this slice authors/renders (ADR-0031). */
export const SCHEMA_MAP_SLUG = "db-schema-map";
