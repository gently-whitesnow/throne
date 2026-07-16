import {
  AlertTriangle,
  ChevronDown,
  CircleCheck,
  ExternalLink,
  GitMerge,
  Loader2
} from "lucide-react";
import { useEffect, useRef, useState } from "react";

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
  cleanup: boolean;
  onCleanupChange: (next: boolean) => void;
  onMerge: (strategy: MergeStrategy, deleteBranch: boolean) => void;
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
  cleanup,
  onCleanupChange,
  onMerge
}: ReviewMergeControlProps) {
  const [strategy, setStrategy] = useState<MergeStrategy>("merge");
  const [deleteBranch, setDeleteBranch] = useState(true);
  const [menuOpen, setMenuOpen] = useState(false);
  const anchorRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const onPointer = (e: MouseEvent) => {
      const t = e.target as Node | null;
      if (t !== null && anchorRef.current?.contains(t) === true) return;
      setMenuOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setMenuOpen(false);
    };
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [menuOpen]);

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
      <span
        className={`inline-flex items-center gap-1.5 rounded-md bg-base-200 px-2 py-1 text-[11px] font-medium ${MERGEABILITY_TONE[status.mergeability]}`}
      >
        {status.mergeability === "mergeable" ? (
          <CircleCheck aria-hidden size={13} strokeWidth={2.25} />
        ) : (
          <AlertTriangle aria-hidden size={13} strokeWidth={2.25} />
        )}
        {MERGEABILITY_LABEL[status.mergeability]}
        {status.checks !== "none" ? (
          <>
            <span aria-hidden className="text-base-content/30">
              ·
            </span>
            <span className={CHECKS_TONE[status.checks]}>
              {CHECKS_LABEL[status.checks]}
            </span>
          </>
        ) : null}
      </span>

      {showProviderLink ? (
        <a
          href={status.html_url ?? undefined}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center gap-1 rounded-md border border-base-300 px-2 py-1 text-[11px] font-medium text-base-content/80 transition-[color,background-color] hover:bg-base-200"
        >
          <ExternalLink aria-hidden size={13} strokeWidth={2} />
          Открыть {kind}
        </a>
      ) : null}

      <div ref={anchorRef} className="relative flex flex-col items-end gap-1">
        <div
          className={`inline-flex items-stretch overflow-hidden rounded-md shadow-sm transition-[opacity,scale] active:scale-[0.98] ${canMerge ? "" : "opacity-40"}`}
        >
          <button
            type="button"
            disabled={!canMerge}
            onClick={() => {
              onMerge(strategy, deleteBranch);
            }}
            className="inline-flex items-center gap-1.5 bg-primary px-3 py-1.5 text-[12px] font-semibold text-primary-content transition-[background-color] hover:bg-primary/90 disabled:cursor-not-allowed"
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
          <button
            type="button"
            aria-label="Настройки мержа"
            aria-expanded={menuOpen}
            aria-haspopup="menu"
            disabled={merging}
            onClick={() => {
              setMenuOpen((v) => !v);
            }}
            className="inline-flex items-center border-l border-primary-content/25 bg-primary px-1.5 text-primary-content transition-[background-color] hover:bg-primary/90 disabled:cursor-not-allowed"
          >
            <ChevronDown
              aria-hidden
              size={14}
              strokeWidth={2.25}
              className={`transition-transform ${menuOpen ? "rotate-180" : ""}`}
            />
          </button>
        </div>
        {menuOpen ? (
          <div
            role="menu"
            aria-label="Настройки мержа"
            className="absolute right-0 top-full z-30 mt-1 w-64 rounded-md border border-base-300 bg-base-100 p-3 text-[12px] shadow-lg"
          >
            <label className="flex flex-col gap-1">
              <span className="text-[11px] font-medium text-base-content/70">
                Стратегия
              </span>
              <select
                aria-label="Стратегия мержа"
                value={strategy}
                onChange={(e) => {
                  setStrategy(e.target.value as MergeStrategy);
                }}
                className="rounded border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content focus:outline-none focus:ring-1 focus:ring-primary/40"
              >
                {(Object.keys(STRATEGY_LABEL) as MergeStrategy[]).map(
                  (value) => (
                    <option key={value} value={value}>
                      {STRATEGY_LABEL[value]}
                    </option>
                  )
                )}
              </select>
            </label>
            <div className="mt-3 flex flex-col gap-2">
              <label className="flex items-center gap-2 text-[12px] text-base-content/80">
                <input
                  type="checkbox"
                  checked={deleteBranch}
                  onChange={(e) => {
                    setDeleteBranch(e.target.checked);
                  }}
                  className="h-3.5 w-3.5 accent-primary"
                />
                Удалить ветку
              </label>
              <label
                className="flex items-center gap-2 text-[12px] text-base-content/80"
                title="Снимите, чтобы оставить сессию и локальное состояние интента после мержа"
              >
                <input
                  type="checkbox"
                  checked={cleanup}
                  onChange={(e) => {
                    onCleanupChange(e.target.checked);
                  }}
                  className="h-3.5 w-3.5 accent-primary"
                />
                Очистить состояние
              </label>
            </div>
          </div>
        ) : null}
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
