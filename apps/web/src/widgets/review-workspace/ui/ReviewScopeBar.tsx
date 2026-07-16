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
  openInIde?: ReactNode;
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
  openInIde,
  onSelectRequest,
  onSelectCommit,
  onClose
}: ReviewScopeBarProps) {
  const kind = changeRequestKindLabel(binding.provider);
  const ref = changeRequestRefLabel(binding);

  const hasRightGroup = openInIde !== undefined || mergeControl !== undefined;

  return (
    <header className="flex items-center gap-3 border-b border-base-300 bg-base-100 px-4 py-2.5">
      <div className="flex min-w-0 items-center gap-2.5">
        <span className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
          <GitPullRequest size={15} strokeWidth={2.25} />
        </span>
        <div className="flex min-w-0 flex-col leading-tight">
          <span className="truncate font-mono text-sm font-semibold text-base-content">
            {repositoryFullName(binding)}
          </span>
          {ref !== null ? (
            <span className="truncate text-[11px] text-base-content/60">
              {ref}
            </span>
          ) : null}
        </div>
      </div>

      <span aria-hidden className="h-6 w-px shrink-0 bg-base-300" />

      <div
        role="group"
        aria-label="Объём diff"
        className="flex items-center gap-1 rounded-md bg-base-200 p-0.5"
      >
        <button
          type="button"
          onClick={onSelectRequest}
          aria-pressed={scope === "request"}
          className={`rounded px-2.5 py-1 text-[12px] font-medium transition-[color,background-color] ${
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

      <div className="ml-auto flex items-center gap-3">
        {hasRightGroup ? (
          <div className="flex items-center gap-2">
            {openInIde}
            {mergeControl}
          </div>
        ) : null}
        {hasRightGroup ? (
          <span aria-hidden className="h-6 w-px shrink-0 bg-base-300" />
        ) : null}
        <button
          type="button"
          aria-label="Закрыть ревью"
          onClick={onClose}
          className="inline-flex h-8 w-8 items-center justify-center rounded-md text-base-content/60 transition-[color,background-color] hover:bg-base-200 hover:text-base-content"
        >
          <X size={18} strokeWidth={2} />
        </button>
      </div>
    </header>
  );
}
