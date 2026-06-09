import { ChevronDown, ChevronRight, ChevronUp, Folder } from "lucide-react";
import { useMemo, useState } from "react";

import {
  countPatchChanges,
  type PullRequestDiffFile,
  type PullRequestDiffFileStatus
} from "@/entities/review-workspace";

import {
  buildFileTree,
  collectDirPaths,
  type FileTreeNode
} from "../lib/build-file-tree";

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

const INDENT_STEP_REM = 0.75;

export function ReviewFilesRail({
  files,
  activePath,
  onSelect,
  onAdjacent
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
      <ul className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
        {tree.map((node) => (
          <TreeRows
            key={node.kind === "dir" ? `d:${node.path}` : node.file.path}
            node={node}
            depth={0}
            activePath={activePath}
            collapsed={collapsed}
            stats={stats}
            onSelect={onSelect}
            onToggleDir={toggleDir}
          />
        ))}
      </ul>
    </nav>
  );
}

function TreeRows({
  node,
  depth,
  activePath,
  collapsed,
  stats,
  onSelect,
  onToggleDir
}: {
  node: FileTreeNode;
  depth: number;
  activePath: string | null;
  collapsed: ReadonlySet<string>;
  stats: Map<string, { additions: number; deletions: number }>;
  onSelect: (path: string) => void;
  onToggleDir: (path: string) => void;
}) {
  const indent = { paddingLeft: `${String(0.5 + depth * INDENT_STEP_REM)}rem` };

  if (node.kind === "dir") {
    const isCollapsed = collapsed.has(node.path);
    return (
      <li>
        <button
          type="button"
          onClick={() => {
            onToggleDir(node.path);
          }}
          aria-expanded={!isCollapsed}
          style={indent}
          className="flex w-full items-center gap-1 py-1 pr-3 text-left text-[12px] text-base-content/80 hover:bg-base-200"
        >
          {isCollapsed ? (
            <ChevronRight aria-hidden size={13} strokeWidth={2} />
          ) : (
            <ChevronDown aria-hidden size={13} strokeWidth={2} />
          )}
          <Folder
            aria-hidden
            size={13}
            strokeWidth={2}
            className="shrink-0 text-base-content/40"
          />
          <span className="min-w-0 flex-1 truncate font-mono">{node.name}</span>
        </button>
        {isCollapsed
          ? null
          : node.children.map((child) => (
              <TreeRows
                key={child.kind === "dir" ? `d:${child.path}` : child.file.path}
                node={child}
                depth={depth + 1}
                activePath={activePath}
                collapsed={collapsed}
                stats={stats}
                onSelect={onSelect}
                onToggleDir={onToggleDir}
              />
            ))}
      </li>
    );
  }

  const file = node.file;
  const active = file.path === activePath;
  const badge = STATUS_BADGE[file.status];
  const count = stats.get(file.path);
  return (
    <li>
      <button
        type="button"
        onClick={() => {
          onSelect(file.path);
        }}
        aria-current={active}
        style={indent}
        className={`flex w-full items-center gap-2 py-1.5 pr-3 text-left text-[12px] ${
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
        <span className="min-w-0 flex-1 truncate font-mono" title={file.path}>
          {node.name}
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
}
