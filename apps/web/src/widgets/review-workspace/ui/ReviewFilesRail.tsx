import { ChevronDown, ChevronUp } from "lucide-react";
import { useMemo, useState } from "react";

import {
  countPatchChanges,
  type PullRequestDiffFile
} from "@/entities/review-workspace";

import { buildFileTree, collectDirPaths } from "../lib/build-file-tree";
import type { FileOrderHint } from "../lib/order-files-by-ai";
import {
  ReviewFilesSortToggle,
  type ReviewFilesSortMode
} from "./ReviewFilesSortToggle";
import { ReviewFilesTreeRow } from "./ReviewFilesTreeRow";

export type { ReviewFilesSortMode };

const EMPTY_HINTS: ReadonlyMap<string, FileOrderHint> = new Map();

interface ReviewFilesRailProps {
  files: PullRequestDiffFile[];
  activePath: string | null;
  onSelect: (path: string) => void;
  onAdjacent: (direction: 1 | -1) => void;
  sortMode: ReviewFilesSortMode;
  /** Toggle is rendered only when this callback is provided AND hints exist. */
  onChangeSortMode?: (mode: ReviewFilesSortMode) => void;
  aiOrderHints?: ReadonlyMap<string, FileOrderHint>;
}

export function ReviewFilesRail({
  files,
  activePath,
  onSelect,
  onAdjacent,
  sortMode,
  onChangeSortMode,
  aiOrderHints
}: ReviewFilesRailProps) {
  const tree = useMemo(() => buildFileTree(files), [files]);
  const stats = useMemo(
    () =>
      new Map(files.map((f) => [f.path, countPatchChanges(f.patch)] as const)),
    [files]
  );

  // Свёрнутые директории; по умолчанию всё раскрыто. При появлении новых
  // директорий в diff'е они остаются раскрытыми, ранее свёрнутые — свёрнутыми.
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(
    () => new Set()
  );
  const allDirs = useMemo(() => collectDirPaths(tree), [tree]);

  const toggleDir = (path: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path);
      else next.add(path);
      return next;
    });
  };

  const allExpanded = collapsed.size === 0;
  const toggleAll = () => {
    setCollapsed(allExpanded ? new Set(allDirs) : new Set());
  };

  const hints = aiOrderHints ?? EMPTY_HINTS;
  const showSortToggle = onChangeSortMode !== undefined && hints.size > 0;
  const activeHints =
    sortMode === "ai-recommended" && hints.size > 0 ? hints : EMPTY_HINTS;

  return (
    <nav
      aria-label="Файлы ревью"
      className="flex h-full min-h-0 flex-col bg-base-100"
    >
      <header className="flex items-center justify-between gap-2 border-b border-base-300 px-3 py-2">
        <button
          type="button"
          onClick={toggleAll}
          className="text-[11px] font-semibold uppercase tracking-wide text-base-content/50 hover:text-base-content"
          title={allExpanded ? "Свернуть все" : "Развернуть все"}
        >
          Файлы · {files.length}
        </button>
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
      {showSortToggle ? (
        <ReviewFilesSortToggle mode={sortMode} onChange={onChangeSortMode} />
      ) : null}
      <ul className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
        {tree.map((node) => (
          <ReviewFilesTreeRow
            key={node.kind === "dir" ? `d:${node.path}` : node.file.path}
            node={node}
            depth={0}
            activePath={activePath}
            collapsed={collapsed}
            stats={stats}
            hints={activeHints}
            onSelect={onSelect}
            onToggleDir={toggleDir}
          />
        ))}
      </ul>
    </nav>
  );
}
