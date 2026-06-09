import { useCallback, useEffect } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";

import {
  hasPullRequest,
  useIntentRepositories
} from "@/entities/repository-binding";

import type { ReviewWorkspaceInitial } from "../model/use-review-workspace";
import { ReviewWorkspaceOverlay } from "./ReviewWorkspaceOverlay";

/**
 * Роут `/intents/:id/review/:bindingId` — открытая ревьюилка, вшитая в URL.
 * scope/commit/файл живут в query, поэтому F5 и шаринг ссылки переоткрывают
 * оверлей в том же состоянии. Отдельной PR-страницы нет: deep-link = этот роут.
 */
export function ReviewWorkspaceRoute() {
  const { id = "", bindingId = "" } = useParams<{
    id: string;
    bindingId: string;
  }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { bindings, isLoading } = useIntentRepositories(id);

  const binding = bindings.find((b) => b.id === bindingId) ?? null;
  const isReviewable = binding !== null && hasPullRequest(binding);

  // Невалидный/исчезнувший binding — возвращаемся к интенту, оверлей не для него.
  useEffect(() => {
    if (!isLoading && !isReviewable && id) {
      void navigate(`/intents/${id}`, { replace: true });
    }
  }, [isLoading, isReviewable, id, navigate]);

  const close = useCallback(() => {
    void navigate(`/intents/${id}`);
  }, [navigate, id]);

  const syncToUrl = useCallback(
    (state: ReviewWorkspaceInitial) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          if (state.scope === "commit" && state.commitSha) {
            next.set("scope", "commit");
            next.set("commit", state.commitSha);
          } else {
            next.delete("scope");
            next.delete("commit");
          }
          if (state.path) next.set("file", state.path);
          else next.delete("file");
          return next;
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );

  if (!isReviewable) return null;

  const initial: ReviewWorkspaceInitial = {
    scope: searchParams.get("scope") === "commit" ? "commit" : "request",
    commitSha: searchParams.get("commit"),
    path: searchParams.get("file")
  };

  return (
    <ReviewWorkspaceOverlay
      key={binding.id}
      intentId={id}
      binding={binding}
      initial={initial}
      onStateChange={syncToUrl}
      onClose={close}
    />
  );
}
