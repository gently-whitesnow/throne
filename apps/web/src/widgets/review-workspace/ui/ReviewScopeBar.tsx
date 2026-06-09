import { GitCommitHorizontal, GitPullRequest, X } from "lucide-react";
import type { ReactNode } from "react";

import {
  changeRequestKindLabel,
  changeRequestRefLabel,
  repositoryFullName,
  type RepositoryBinding
} from "@/entities/repository-binding";
import type {
  PullRequestCommit,
  ReviewDiffScope
} from "@/entities/review-workspace";

interface ReviewScopeBarProps {
  binding: RepositoryBinding;
  scope: ReviewDiffScope;
  selectedCommitSha: string | null;
  commits: PullRequestCommit[];
  commitsLoading: boolean;
  mergeControl?: ReactNode;
  onSelectRequest: () => void;
  onSelectCommit: (sha: string) => void;
  onClose: () => void;
}

function commitLabel(commit: PullRequestCommit): string {
  const title = commit.message.split("\n", 1)[0];
  return `${commit.sha.slice(0, 7)} · ${title}`;
}

export function ReviewScopeBar({
  binding,
  scope,
  selectedCommitSha,
  commits,
  commitsLoading,
  mergeControl,
  onSelectRequest,
  onSelectCommit,
  onClose
}: ReviewScopeBarProps) {
  const kind = changeRequestKindLabel(binding.provider);
  const ref = changeRequestRefLabel(binding);

  return (
    <header className="flex items-center gap-3 border-b border-base-300 bg-base-100 px-4 py-2">
      <span className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
        <GitPullRequest size={15} strokeWidth={2.25} />
      </span>
      <div className="flex min-w-0 flex-col">
        <span className="truncate font-mono text-sm font-semibold text-base-content">
          {repositoryFullName(binding)}
        </span>
        {ref !== null ? (
          <span className="text-[11px] text-base-content/60">{ref}</span>
        ) : null}
      </div>

      <div
        role="group"
        aria-label="Объём diff"
        className="ml-4 flex items-center gap-1 rounded-md bg-base-200 p-0.5"
      >
        <button
          type="button"
          onClick={onSelectRequest}
          aria-pressed={scope === "request"}
          className={`rounded px-2.5 py-1 text-[12px] font-medium ${
            scope === "request"
              ? "bg-base-100 text-base-content shadow-sm"
              : "text-base-content/60 hover:text-base-content"
          }`}
        >
          Весь {kind}
        </button>
        <span className="flex items-center gap-1 px-1 text-base-content/40">
          <GitCommitHorizontal aria-hidden size={14} strokeWidth={2} />
        </span>
        <select
          aria-label="Diff по коммиту"
          value={scope === "commit" ? (selectedCommitSha ?? "") : ""}
          disabled={commitsLoading || commits.length === 0}
          onChange={(e) => {
            if (e.target.value.length > 0) onSelectCommit(e.target.value);
          }}
          className="max-w-[20rem] truncate rounded bg-transparent px-1 py-1 text-[12px] text-base-content focus:outline-none disabled:text-base-content/40"
        >
          <option value="">
            {commitsLoading
              ? "Загружаем коммиты…"
              : commits.length === 0
                ? "Коммиты недоступны"
                : "Коммит…"}
          </option>
          {commits.map((commit) => (
            <option key={commit.sha} value={commit.sha}>
              {commitLabel(commit)}
            </option>
          ))}
        </select>
      </div>

      {mergeControl !== undefined ? (
        <div className="ml-auto flex items-center">{mergeControl}</div>
      ) : null}

      <button
        type="button"
        aria-label="Закрыть ревью"
        onClick={onClose}
        className={`${mergeControl !== undefined ? "ml-2" : "ml-auto"} rounded-md p-1.5 text-base-content/60 hover:bg-base-200 hover:text-base-content`}
      >
        <X size={18} strokeWidth={2} />
      </button>
    </header>
  );
}
