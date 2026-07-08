import { useEffect, useMemo, useState } from "react";

import {
  hasPullRequest,
  type RepositoryBinding
} from "@/entities/repository-binding";

export interface ReviewTargetSelection {
  reviewTargets: RepositoryBinding[];
  hasReviewTarget: boolean;
  selectedReviewTargetId: string;
  setSelectedReviewBindingId: (bindingId: string) => void;
}

export function useReviewTargetSelection(
  bindings: readonly RepositoryBinding[]
): ReviewTargetSelection {
  const [selectedReviewBindingId, setSelectedReviewBindingId] = useState("");
  const reviewTargets = useMemo(
    () => bindings.filter(hasPullRequest),
    [bindings]
  );

  useEffect(() => {
    if (reviewTargets.length < 2) {
      setSelectedReviewBindingId("");
      return;
    }
    if (
      !reviewTargets.some((binding) => binding.id === selectedReviewBindingId)
    ) {
      setSelectedReviewBindingId(reviewTargets[0].id);
    }
  }, [reviewTargets, selectedReviewBindingId]);

  const selectedReviewTargetId =
    reviewTargets.length > 1 &&
    reviewTargets.some((binding) => binding.id === selectedReviewBindingId)
      ? selectedReviewBindingId
      : (reviewTargets[0]?.id ?? "");
  return {
    reviewTargets,
    hasReviewTarget: reviewTargets.length > 0,
    selectedReviewTargetId,
    setSelectedReviewBindingId
  };
}
