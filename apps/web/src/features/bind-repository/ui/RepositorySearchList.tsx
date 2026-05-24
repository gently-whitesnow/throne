import { Check, Loader2, Lock } from "lucide-react";

import type { GitRepositoryRef } from "@/entities/repository-binding";

interface RepositorySearchListProps {
  results: GitRepositoryRef[];
  isLoading: boolean;
  error: Error | null;
  selectedFullName: string | null;
  onSelect: (repo: GitRepositoryRef) => void;
}

/**
 * Scrollable list of `GitRepositoryRef` rows. Keeps the modal layout stable
 * even when the search is empty — we always render the same container so the
 * dialog doesn't jump as the user types.
 */
export function RepositorySearchList({
  results,
  isLoading,
  error,
  selectedFullName,
  onSelect
}: RepositorySearchListProps) {
  if (error) {
    return (
      <div
        role="alert"
        className="rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
        data-testid="bind-repository-search-error"
      >
        Не удалось загрузить репозитории: {error.message}
      </div>
    );
  }

  if (isLoading && results.length === 0) {
    return (
      <div
        className="flex h-40 items-center justify-center text-sm text-base-content/60"
        data-testid="bind-repository-search-loading"
      >
        <Loader2 aria-hidden size={16} className="mr-2 animate-spin" />
        Ищем репозитории…
      </div>
    );
  }

  if (results.length === 0) {
    return (
      <div
        className="flex h-40 items-center justify-center text-center text-sm text-base-content/60"
        data-testid="bind-repository-search-empty"
      >
        Ничего не найдено. Проверьте, что `gh` авторизован и у пользователя есть
        доступ к нужным репозиториям.
      </div>
    );
  }

  return (
    <ul
      className="m-0 flex max-h-72 list-none flex-col gap-1 overflow-y-auto p-0"
      data-testid="bind-repository-search-list"
      aria-busy={isLoading}
    >
      {results.map((repo) => {
        const fullName = repo.full_name;
        const isSelected = selectedFullName === fullName;
        return (
          <li key={fullName} className="m-0 p-0">
            <button
              type="button"
              onClick={() => {
                onSelect(repo);
              }}
              data-testid={`bind-repository-row-${fullName}`}
              aria-pressed={isSelected}
              className={`flex w-full items-start justify-between gap-3 rounded-md border px-3 py-2 text-left transition-colors ${
                isSelected
                  ? "border-primary/60 bg-primary/10"
                  : "border-base-300 bg-base-100 hover:border-primary/40 hover:bg-base-200"
              }`}
            >
              <span className="flex min-w-0 flex-col gap-0.5">
                <span className="flex items-center gap-1.5 truncate font-mono text-sm font-semibold">
                  {fullName}
                  {repo.private ? (
                    <Lock
                      aria-label="private"
                      size={12}
                      className="text-base-content/50"
                    />
                  ) : null}
                </span>
                <span className="truncate text-xs text-base-content/60">
                  default: {repo.default_branch}
                  {repo.description ? ` · ${repo.description}` : ""}
                </span>
              </span>
              {isSelected ? (
                <Check aria-hidden size={16} className="mt-0.5 text-primary" />
              ) : null}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
