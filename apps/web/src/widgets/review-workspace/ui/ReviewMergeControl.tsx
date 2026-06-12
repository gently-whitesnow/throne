import {
  AlertTriangle,
  CircleCheck,
  ExternalLink,
  GitMerge,
  Loader2
} from "lucide-react";
import { useState } from "react";

import type {
  MergeStrategy,
  PullRequestChecksState,
  PullRequestMergeability,
  PullRequestMergeStatus
} from "@/entities/review-workspace";

interface ReviewMergeControlProps {
  kind: "PR" | "MR";
  status: PullRequestMergeStatus | null;
  statusLoading: boolean;
  merging: boolean;
  mergeError: string | null;
  onMerge: (
    strategy: MergeStrategy,
    deleteBranch: boolean,
    cleanupAfterMerge: boolean
  ) => void;
}

const MERGEABILITY_LABEL: Record<PullRequestMergeability, string> = {
  mergeable: "Готов к мержу",
  conflicting: "Конфликты",
  blocked: "Заблокирован",
  behind: "Отстаёт от базы",
  checking: "Проверяется…",
  unknown: "Состояние неизвестно"
};

const MERGEABILITY_TONE: Record<PullRequestMergeability, string> = {
  mergeable: "text-success",
  conflicting: "text-error",
  blocked: "text-error",
  behind: "text-warning",
  checking: "text-base-content/60",
  unknown: "text-base-content/60"
};

const CHECKS_LABEL: Record<PullRequestChecksState, string> = {
  none: "",
  pending: "Проверки идут",
  passing: "Проверки прошли",
  failing: "Проверки упали",
  unknown: "Проверки: ?"
};

const CHECKS_TONE: Record<PullRequestChecksState, string> = {
  none: "",
  pending: "text-warning",
  passing: "text-success",
  failing: "text-error",
  unknown: "text-base-content/60"
};

const STRATEGY_LABEL: Record<MergeStrategy, string> = {
  merge: "Merge commit",
  squash: "Squash",
  rebase: "Rebase"
};

export function ReviewMergeControl({
  kind,
  status,
  statusLoading,
  merging,
  mergeError,
  onMerge
}: ReviewMergeControlProps) {
  const [strategy, setStrategy] = useState<MergeStrategy>("merge");
  const [deleteBranch, setDeleteBranch] = useState(true);
  const [cleanupAfterMerge, setCleanupAfterMerge] = useState(true);

  if (statusLoading && status === null) {
    return (
      <span className="flex items-center gap-1.5 text-[11px] text-base-content/60">
        <Loader2
          aria-hidden
          size={13}
          strokeWidth={2}
          className="animate-spin"
        />
        Проверяем мерж…
      </span>
    );
  }
  if (status === null) return null;

  const canMerge = status.mergeability === "mergeable" && !merging;
  const showProviderLink =
    status.mergeability !== "mergeable" && status.html_url != null;

  return (
    <div className="flex items-center gap-3">
      <div className="flex flex-col items-end gap-0.5">
        <span
          className={`flex items-center gap-1 text-[11px] font-medium ${MERGEABILITY_TONE[status.mergeability]}`}
        >
          {status.mergeability === "mergeable" ? (
            <CircleCheck aria-hidden size={13} strokeWidth={2.25} />
          ) : (
            <AlertTriangle aria-hidden size={13} strokeWidth={2.25} />
          )}
          {MERGEABILITY_LABEL[status.mergeability]}
        </span>
        {status.checks !== "none" ? (
          <span className={`text-[10px] ${CHECKS_TONE[status.checks]}`}>
            {CHECKS_LABEL[status.checks]}
          </span>
        ) : null}
      </div>

      {showProviderLink ? (
        <a
          href={status.html_url ?? undefined}
          target="_blank"
          rel="noreferrer"
          className="flex items-center gap-1 rounded-md border border-base-300 px-2 py-1 text-[11px] font-medium text-base-content/80 hover:bg-base-200"
        >
          <ExternalLink aria-hidden size={13} strokeWidth={2} />
          Открыть {kind}
        </a>
      ) : null}

      <select
        aria-label="Стратегия мержа"
        value={strategy}
        disabled={merging}
        onChange={(e) => {
          setStrategy(e.target.value as MergeStrategy);
        }}
        className="rounded bg-base-200 px-1.5 py-1 text-[11px] text-base-content focus:outline-none disabled:opacity-50"
      >
        {(Object.keys(STRATEGY_LABEL) as MergeStrategy[]).map((value) => (
          <option key={value} value={value}>
            {STRATEGY_LABEL[value]}
          </option>
        ))}
      </select>

      <label className="flex items-center gap-1 text-[11px] text-base-content/70">
        <input
          type="checkbox"
          checked={deleteBranch}
          disabled={merging}
          onChange={(e) => {
            setDeleteBranch(e.target.checked);
          }}
          className="h-3.5 w-3.5 accent-primary"
        />
        Удалить ветку
      </label>

      <label
        className="flex items-center gap-1 text-[11px] text-base-content/70"
        title="Снимите, чтобы оставить сессию и локальное состояние интента после мержа"
      >
        <input
          type="checkbox"
          checked={cleanupAfterMerge}
          disabled={merging}
          onChange={(e) => {
            setCleanupAfterMerge(e.target.checked);
          }}
          className="h-3.5 w-3.5 accent-primary"
        />
        Очистить состояние после мержа
      </label>

      <div className="flex flex-col items-end gap-0.5">
        <button
          type="button"
          disabled={!canMerge}
          onClick={() => {
            onMerge(strategy, deleteBranch, cleanupAfterMerge);
          }}
          className="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-[12px] font-semibold text-primary-content hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-40"
        >
          {merging ? (
            <Loader2
              aria-hidden
              size={14}
              strokeWidth={2}
              className="animate-spin"
            />
          ) : (
            <GitMerge aria-hidden size={14} strokeWidth={2.25} />
          )}
          Смержить
        </button>
        {mergeError !== null ? (
          <span
            role="alert"
            className="max-w-[16rem] text-right text-[10px] text-error"
          >
            {mergeError}
          </span>
        ) : null}
      </div>
    </div>
  );
}
