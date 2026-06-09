import { ChevronDown, ChevronUp } from "lucide-react";
import { useMemo } from "react";

import {
  countPatchChanges,
  type PullRequestDiffFile,
  type PullRequestDiffFileStatus
} from "@/entities/review-workspace";

interface ReviewFilesRailProps {
  files: PullRequestDiffFile[];
  activePath: string | null;
  onSelect: (path: string) => void;
  onAdjacent: (direction: 1 | -1) => void;
}

const STATUS_BADGE: Record<
  PullRequestDiffFileStatus,
  { letter: string; cls: string }
> = {
  added: { letter: "A", cls: "text-success" },
  modified: { letter: "M", cls: "text-warning" },
  removed: { letter: "D", cls: "text-error" },
  renamed: { letter: "R", cls: "text-base-content/60" },
  copied: { letter: "C", cls: "text-base-content/60" }
};

export function ReviewFilesRail({
  files,
  activePath,
  onSelect,
  onAdjacent
}: ReviewFilesRailProps) {
  const stats = useMemo(
    () =>
      new Map(files.map((f) => [f.path, countPatchChanges(f.patch)] as const)),
    [files]
  );

  return (
    <nav
      aria-label="Файлы ревью"
      className="flex h-full min-h-0 w-72 shrink-0 flex-col border-r border-base-300 bg-base-100"
    >
      <header className="flex items-center justify-between gap-2 border-b border-base-300 px-3 py-2">
        <span className="text-[11px] font-semibold uppercase tracking-wide text-base-content/50">
          Файлы · {files.length}
        </span>
        <span className="flex items-center gap-1">
          <button
            type="button"
            aria-label="Предыдущий файл"
            onClick={() => {
              onAdjacent(-1);
            }}
            className="rounded p-1 text-base-content/50 hover:bg-base-200 hover:text-base-content"
          >
            <ChevronUp size={14} strokeWidth={2} />
          </button>
          <button
            type="button"
            aria-label="Следующий файл"
            onClick={() => {
              onAdjacent(1);
            }}
            className="rounded p-1 text-base-content/50 hover:bg-base-200 hover:text-base-content"
          >
            <ChevronDown size={14} strokeWidth={2} />
          </button>
        </span>
      </header>
      <ul className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
        {files.map((file) => {
          const active = file.path === activePath;
          const badge = STATUS_BADGE[file.status];
          const count = stats.get(file.path);
          return (
            <li key={file.path}>
              <button
                type="button"
                onClick={() => {
                  onSelect(file.path);
                }}
                aria-current={active}
                className={`flex w-full items-center gap-2 px-3 py-1.5 text-left text-[12px] ${
                  active
                    ? "bg-primary/10 text-primary"
                    : "text-base-content hover:bg-base-200"
                }`}
              >
                <span
                  aria-hidden
                  className={`w-3 shrink-0 text-center font-mono font-semibold ${badge.cls}`}
                >
                  {badge.letter}
                </span>
                <span
                  className="min-w-0 flex-1 truncate font-mono"
                  title={file.path}
                >
                  {file.path}
                </span>
                {count !== undefined ? (
                  <span className="shrink-0 font-mono text-[10px] tabular-nums">
                    <span className="text-success">+{count.additions}</span>{" "}
                    <span className="text-error">−{count.deletions}</span>
                  </span>
                ) : null}
              </button>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
