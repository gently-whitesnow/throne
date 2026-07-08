import { MessageSquare } from "lucide-react";

import {
  hasPullRequest,
  useIntentRepositories
} from "@/entities/repository-binding";
import { CollapsibleSection } from "@/shared/ui";

import { PullRequestCommentsCard } from "./PullRequestCommentsCard";

interface PullRequestCommentsSectionProps {
  intentId: string;
}

/**
 * Секция «PR comments» на странице интента.
 *
 * Под каждым binding'ом с привязанным PR рисуется карточка с лентой
 * review-комментариев и кнопкой `Обновить` (sync). Binding'и без PR здесь не
 * показываются — для них видна только основная секция «Репозитории».
 *
 * Если у интента нет ни одного PR-binding'а, всю секцию скрываем, чтобы не
 * захламлять страницу пустыми заголовками.
 */
export function PullRequestCommentsSection({
  intentId
}: PullRequestCommentsSectionProps) {
  const { bindings } = useIntentRepositories(intentId);
  const prBindings = bindings.filter(hasPullRequest);

  if (prBindings.length === 0) {
    return null;
  }

  return (
    <CollapsibleSection
      aria-label="PR comments"
      data-testid="pr-comments-section"
      icon={<MessageSquare size={14} strokeWidth={2} />}
      title="PR comments"
      count={prBindings.length}
    >
      <div className="flex flex-col gap-3 pt-2">
        {prBindings.map((binding) => (
          <PullRequestCommentsCard
            key={binding.id}
            intentId={intentId}
            binding={binding}
          />
        ))}
      </div>
    </CollapsibleSection>
  );
}
