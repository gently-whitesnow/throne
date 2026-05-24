import { MessageSquare } from "lucide-react";

import {
  hasPullRequest,
  useIntentRepositories
} from "@/entities/repository-binding";

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
    <section
      aria-label="PR comments"
      className="mt-6 flex flex-col gap-3"
      data-testid="pr-comments-section"
    >
      <header className="flex items-center gap-2">
        <MessageSquare
          aria-hidden
          size={16}
          strokeWidth={2}
          className="text-base-content/60"
        />
        <h2 className="m-0 text-sm font-semibold text-base-content">
          PR comments
        </h2>
        <span className="rounded-full bg-base-200 px-2 py-0.5 text-[11px] font-medium text-base-content/60">
          {prBindings.length}
        </span>
      </header>
      <div className="flex flex-col gap-3">
        {prBindings.map((binding) => (
          <PullRequestCommentsCard
            key={binding.id}
            intentId={intentId}
            binding={binding}
          />
        ))}
      </div>
    </section>
  );
}
