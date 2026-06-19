import type { RepositoriesComponents } from "@/shared/api";

export type PullRequestArtifact =
  RepositoriesComponents["schemas"]["PullRequestArtifactDto"];

export type PullRequestArtifactRender =
  RepositoriesComponents["schemas"]["PullRequestArtifactRender"];

export type PullRequestArtifactSource =
  RepositoriesComponents["schemas"]["PullRequestArtifactSource"];

export type ReviewRecommendationContent =
  RepositoriesComponents["schemas"]["ReviewRecommendationContent"];

export type ReviewFileOrderEntry =
  RepositoriesComponents["schemas"]["ReviewFileOrderEntry"];

export type ReviewFileRisk =
  RepositoriesComponents["schemas"]["ReviewFileRisk"];

/** Stable artifact type label for AI-driven review recommendations (ADR-0031). */
export const REVIEW_RECOMMENDATION_ARTIFACT_TYPE = "review_recommendation";
