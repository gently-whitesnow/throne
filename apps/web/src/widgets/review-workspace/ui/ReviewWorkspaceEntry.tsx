import { Maximize2, SquareChevronRight } from "lucide-react";
import { Link } from "react-router-dom";

import {
  changeRequestRefLabel,
  hasPullRequest,
  repositoryFullName,
  useIntentRepositories
} from "@/entities/repository-binding";

interface ReviewWorkspaceEntryProps {
  intentId: string;
}

/**
 * Entry-панель ревьюилки в `placement=review`. Под каждым binding'ом с
 * привязанным PR/MR — ссылка на роут ревьюилки (`/intents/:id/review/:bindingId`),
 * открывающий fullscreen review workspace в том же окне Throne. Ссылка
 * шарящаяся и переживает F5. Без PR/MR-binding'ов секция скрыта.
 */
export function ReviewWorkspaceEntry({ intentId }: ReviewWorkspaceEntryProps) {
  const { bindings } = useIntentRepositories(intentId);

  const prBindings = bindings.filter(hasPullRequest);
  if (prBindings.length === 0) return null;

  return (
    <section
      aria-label="Review workspace"
      className="mt-6 flex flex-col gap-2"
      data-testid="review-workspace-entry"
    >
      <header className="flex items-center gap-2">
        <SquareChevronRight
          aria-hidden
          size={16}
          strokeWidth={2}
          className="text-base-content/60"
        />
        <h2 className="m-0 text-sm font-semibold text-base-content">
          Review workspace
        </h2>
      </header>
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {prBindings.map((binding) => (
          <li
            key={binding.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-base-300 bg-base-100 px-4 py-2.5"
          >
            <span className="flex min-w-0 flex-wrap items-center gap-2 text-xs">
              <span className="truncate font-mono text-sm font-semibold text-base-content">
                {repositoryFullName(binding)}
              </span>
              <span className="text-base-content/60">
                {changeRequestRefLabel(binding)}
              </span>
            </span>
            <Link
              to={`/intents/${intentId}/review/${binding.id}`}
              className="btn btn-sm btn-primary"
            >
              <Maximize2 aria-hidden size={14} strokeWidth={2} />
              Открыть ревью
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
