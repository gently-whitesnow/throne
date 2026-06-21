import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";

import {
  BindRepositorySearchControls,
  RepositorySearchList,
  useRepositorySearch,
  type SearchScope
} from "@/features/bind-repository";
import type { GitProvider } from "@/entities/repository-binding";
import { Button, Modal } from "@/shared/ui";

interface AddDefaultRepositoryModalProps {
  open: boolean;
  onClose: () => void;
  onPicked: (pick: {
    provider: GitProvider;
    host?: string;
    owner: string;
    repo: string;
    project_id?: number | null;
    default_branch: string;
  }) => void;
}

const DEFAULT_PROVIDER: GitProvider = "github";

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
  const [provider, setProvider] = useState<GitProvider>(DEFAULT_PROVIDER);
  const [query, setQuery] = useState("");
  const [scope, setScope] = useState<SearchScope>("mine");
  const titleId = useId();

  const { results, isLoading, error } = useRepositorySearch(
    query,
    scope,
    open,
    provider
  );

  useEffect(() => {
    if (open) return;
    setProvider(DEFAULT_PROVIDER);
    setQuery("");
    setScope("mine");
  }, [open]);

  if (!open) return null;

  return (
    <Modal
      onClose={onClose}
      labelledBy={titleId}
      boxClassName="max-h-[min(720px,calc(100vh-32px))] w-full max-w-2xl"
    >
      <div className="mb-4 flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
            Default repository
          </p>
          <h3 id={titleId} className="m-0 text-lg font-semibold leading-tight">
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
          provider={provider}
          onProviderChange={setProvider}
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
          onSelect={(repo) => {
            onPicked({
              provider: repo.provider,
              host: repo.host,
              owner: repo.owner,
              repo: repo.repo,
              project_id: repo.project_id,
              default_branch: repo.default_branch
            });
            onClose();
          }}
        />
        <div className="flex justify-end">
          <Button onClick={onClose}>Закрыть</Button>
        </div>
      </div>
    </Modal>
  );
}
