import { useQueryClient } from "@tanstack/react-query";
import { Loader2, Lock, Plus, X } from "lucide-react";
import { useEffect, useState } from "react";

import { useCapabilityEnabled } from "@/entities/capability";
import {
  createRepository,
  repositoriesQueryKeys,
  type Repository
} from "@/entities/repository";
import type {
  GitProvider,
  GitRepositoryRef
} from "@/entities/repository-binding";
import { HttpError } from "@/shared/api";

import { useRepositorySearch } from "../model/use-repository-search";

interface AddRepositoryModalProps {
  onClose: () => void;
  onAdded: (repo: Repository) => void;
}

const DEFAULT_PROVIDER: GitProvider = "github";

/**
 * Manual registry add via the Slice 1 autocomplete. `createRepository` is
 * idempotent (200 when the coordinate already exists), so re-adding a known
 * repo simply resolves to its row.
 */
export function AddRepositoryModal({
  onClose,
  onAdded
}: AddRepositoryModalProps) {
  const queryClient = useQueryClient();
  const [provider, setProvider] = useState<GitProvider>(DEFAULT_PROVIDER);
  const [query, setQuery] = useState("");
  const [involved, setInvolved] = useState(false);
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  // GitLab gated behind the `gitlab` capability (ADR-0032 § 8).
  const gitlabEnabled = useCapabilityEnabled("gitlab");
  useEffect(() => {
    if (!gitlabEnabled && provider === "gitlab") {
      setProvider("github");
    }
  }, [gitlabEnabled, provider]);
  const {
    results,
    isLoading,
    error: searchError
  } = useRepositorySearch(
    query,
    involved ? "involved" : "mine",
    true,
    provider
  );

  const add = async (ref: GitRepositoryRef) => {
    if (submitting !== null) return;
    setSubmitting(ref.full_name);
    setError(null);
    try {
      const repo = await createRepository({
        provider: ref.provider,
        // host comes from the search ref: GitHub → "github.com", GitLab → the
        // configured self-managed host (always populated by the GitLab search).
        // Only GitHub has a safe fixed default; for GitLab we send the host as-is
        // (empty → server rejects) instead of guessing "gitlab.com", which would
        // silently bind a self-managed repo to the wrong host.
        host: ref.host ?? (ref.provider === "github" ? "github.com" : ""),
        owner: ref.owner,
        repo: ref.repo,
        project_id: ref.project_id
      });
      await queryClient.invalidateQueries({
        queryKey: repositoriesQueryKeys.list()
      });
      onAdded(repo);
    } catch (err: unknown) {
      setError(
        err instanceof HttpError
          ? `Не удалось добавить (${String(err.status)}).`
          : "Не удалось добавить репозиторий."
      );
      setSubmitting(null);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center bg-neutral/40 p-4 pt-[10vh]"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="flex max-h-[80vh] w-[min(520px,100vw)] flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-6 shadow-lg"
        role="dialog"
        aria-modal="true"
        aria-label="Добавить репозиторий"
        onClick={(e) => {
          e.stopPropagation();
        }}
      >
        <header className="flex items-center justify-between gap-3">
          <h3 className="m-0 text-base font-semibold">Добавить репозиторий</h3>
          <button
            type="button"
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </header>

        <div className="join w-fit" role="radiogroup" aria-label="Git provider">
          <button
            type="button"
            className={`btn join-item btn-xs ${
              provider === "github" ? "btn-primary" : "btn-ghost"
            }`}
            aria-pressed={provider === "github"}
            onClick={() => {
              setProvider("github");
            }}
          >
            GitHub
          </button>
          {gitlabEnabled ? (
            <button
              type="button"
              className={`btn join-item btn-xs ${
                provider === "gitlab" ? "btn-primary" : "btn-ghost"
              }`}
              aria-pressed={provider === "gitlab"}
              onClick={() => {
                setProvider("gitlab");
              }}
            >
              GitLab
            </button>
          ) : null}
        </div>

        <input
          type="text"
          autoFocus
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
          }}
          placeholder="owner/repo или часть имени…"
          aria-label="Поиск репозитория"
          className="input input-bordered w-full font-mono text-sm"
        />
        <label className="flex items-center gap-2 text-xs text-base-content/70">
          <input
            type="checkbox"
            className="checkbox checkbox-sm"
            checked={involved}
            onChange={(e) => {
              setInvolved(e.target.checked);
            }}
          />
          Включая репозитории организаций и коллабораций
        </label>

        {error ? (
          <p role="alert" className="m-0 text-sm text-error">
            {error}
          </p>
        ) : null}

        <SearchResults
          results={results}
          isLoading={isLoading}
          error={searchError}
          submitting={submitting}
          onPick={(ref) => void add(ref)}
        />
      </div>
    </div>
  );
}

interface SearchResultsProps {
  results: GitRepositoryRef[];
  isLoading: boolean;
  error: Error | null;
  submitting: string | null;
  onPick: (ref: GitRepositoryRef) => void;
}

function SearchResults({
  results,
  isLoading,
  error,
  submitting,
  onPick
}: SearchResultsProps) {
  if (error) {
    return (
      <p role="alert" className="m-0 text-sm text-error">
        Не удалось загрузить репозитории: {error.message}
      </p>
    );
  }
  if (isLoading && results.length === 0) {
    return (
      <div className="flex h-32 items-center justify-center text-sm text-base-content/60">
        <Loader2 aria-hidden size={16} className="mr-2 animate-spin" /> Ищем…
      </div>
    );
  }
  if (results.length === 0) {
    return (
      <p className="flex h-32 items-center justify-center text-center text-sm text-base-content/60">
        Ничего не найдено. Проверьте, что `gh` авторизован.
      </p>
    );
  }
  return (
    <ul className="m-0 flex max-h-72 list-none flex-col gap-1 overflow-y-auto p-0">
      {results.map((ref) => (
        <li key={ref.full_name} className="m-0 p-0">
          <button
            type="button"
            disabled={submitting !== null}
            onClick={() => {
              onPick(ref);
            }}
            className="flex w-full items-center justify-between gap-3 rounded-md border border-base-300 bg-base-100 px-3 py-2 text-left transition-colors hover:border-primary/40 hover:bg-base-200 disabled:opacity-60"
          >
            <span className="flex min-w-0 items-center gap-1.5 truncate font-mono text-sm font-semibold">
              {ref.full_name}
              {ref.private ? (
                <Lock
                  aria-label="private"
                  size={12}
                  className="text-base-content/50"
                />
              ) : null}
            </span>
            {submitting === ref.full_name ? (
              <Loader2
                aria-hidden
                size={16}
                className="animate-spin text-primary"
              />
            ) : (
              <Plus aria-hidden size={16} className="text-base-content/50" />
            )}
          </button>
        </li>
      ))}
    </ul>
  );
}
