import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";

import {
  BindRepositorySearchControls,
  RepositorySearchList,
  useRepositorySearch,
  type SearchScope
} from "@/features/bind-repository";
import { Button } from "@/shared/ui";

interface AddDefaultRepositoryModalProps {
  open: boolean;
  onClose: () => void;
  onPicked: (pick: {
    provider: "github";
    owner: string;
    repo: string;
    default_branch: string;
  }) => void;
}

/**
 * Модалка добавления репозитория в `Tag.default_repositories`. Переиспользует
 * Slice 1 search-стек (`useRepositorySearch` + `BindRepositorySearchControls` +
 * `RepositorySearchList`); собственного branch/PR-поля нет — фиксируем upstream
 * default branch выбранного репозитория, чтобы Run pre-flight binding не падал на
 * угаданный `main` для репозитория с trunk `master` (ADR-0024 § 1).
 */
export function AddDefaultRepositoryModal({
  open,
  onClose,
  onPicked
}: AddDefaultRepositoryModalProps) {
  const [query, setQuery] = useState("");
  const [scope, setScope] = useState<SearchScope>("mine");
  const titleId = useId();

  const { results, isLoading, error } = useRepositorySearch(query, scope, open);

  useEffect(() => {
    if (open) return;
    setQuery("");
    setScope("mine");
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const handler = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    };
    window.addEventListener("keydown", handler);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      window.removeEventListener("keydown", handler);
      document.body.style.overflow = previousOverflow;
    };
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div
      className="modal modal-open modal-bottom sm:modal-middle"
      role="presentation"
      onClick={onClose}
    >
      <div
        className="modal-box max-h-[min(720px,calc(100vh-32px))] w-full max-w-2xl border border-base-300 bg-base-100"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => {
          e.stopPropagation();
        }}
      >
        <div className="mb-4 flex items-start justify-between gap-4">
          <div className="flex flex-col gap-1">
            <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
              Default repository
            </p>
            <h3
              id={titleId}
              className="m-0 text-lg font-semibold leading-tight"
            >
              Выберите репозиторий
            </h3>
            <p className="m-0 text-xs text-base-content/60">
              Будет добавлен в default-список тега. На Run pre-flight'е каждый
              интент с этим тегом получит binding к этому репозиторию.
            </p>
          </div>
          <button
            type="button"
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </div>

        <div className="flex flex-col gap-4">
          <BindRepositorySearchControls
            query={query}
            onQueryChange={setQuery}
            scope={scope}
            onScopeChange={setScope}
            disabled={false}
          />
          <RepositorySearchList
            results={results}
            isLoading={isLoading}
            error={error}
            selectedFullName={null}
            onSelect={(repo) => {
              onPicked({
                provider: "github",
                owner: repo.owner,
                repo: repo.repo,
                default_branch: repo.default_branch
              });
              onClose();
            }}
          />
          <div className="flex justify-end">
            <Button onClick={onClose}>Закрыть</Button>
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
}
