import { ChevronDown, ChevronRight, Folder } from "lucide-react";

import type { ReviewFileRisk } from "@/entities/pull-request-artifact";
import type { PullRequestDiffFileStatus } from "@/entities/review-workspace";

import type { FileTreeNode } from "../lib/build-file-tree";
import type { FileOrderHint } from "../lib/order-files-by-ai";

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

interface RiskMeta {
  label: string;
  ink: string;
}

const RISK_META: Record<ReviewFileRisk, RiskMeta> = {
  high: {
    label: "Высокий риск",
    ink: "var(--color-status-danger-ink)"
  },
  medium: {
    label: "Средний риск",
    ink: "var(--color-status-attention-ink)"
  },
  low: {
    label: "Низкий риск",
    ink: "var(--color-status-success-ink)"
  }
};

const INDENT_STEP_REM = 0.75;

export interface ReviewFilesTreeRowProps {
  node: FileTreeNode;
  depth: number;
  activePath: string | null;
  collapsed: ReadonlySet<string>;
  stats: Map<string, { additions: number; deletions: number }>;
  hints: ReadonlyMap<string, FileOrderHint>;
  onSelect: (path: string) => void;
  onToggleDir: (path: string) => void;
}

export function ReviewFilesTreeRow(props: ReviewFilesTreeRowProps) {
  const { node, depth } = props;
  const indent = { paddingLeft: `${String(0.5 + depth * INDENT_STEP_REM)}rem` };

  if (node.kind === "dir") {
    return <DirRow {...props} indent={indent} />;
  }
  return <FileRow {...props} indent={indent} />;
}

function DirRow({
  node,
  depth,
  activePath,
  collapsed,
  stats,
  hints,
  onSelect,
  onToggleDir,
  indent
}: ReviewFilesTreeRowProps & { indent: { paddingLeft: string } }) {
  if (node.kind !== "dir") return null;
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
            <ReviewFilesTreeRow
              key={child.kind === "dir" ? `d:${child.path}` : child.file.path}
              node={child}
              depth={depth + 1}
              activePath={activePath}
              collapsed={collapsed}
              stats={stats}
              hints={hints}
              onSelect={onSelect}
              onToggleDir={onToggleDir}
            />
          ))}
    </li>
  );
}

function FileRow({
  node,
  activePath,
  stats,
  hints,
  onSelect,
  indent
}: ReviewFilesTreeRowProps & { indent: { paddingLeft: string } }) {
  if (node.kind !== "file") return null;
  const file = node.file;
  const active = file.path === activePath;
  const badge = STATUS_BADGE[file.status];
  const count = stats.get(file.path);
  const hint = hints.get(file.path);
  const riskMeta = hint?.risk != null ? RISK_META[hint.risk] : null;
  const titleText = buildFileTitle(file.path, hint);
  return (
    <li>
      <button
        type="button"
        onClick={() => {
          onSelect(file.path);
        }}
        aria-current={active}
        style={indent}
        title={titleText}
        className={`flex w-full items-center gap-2 py-1.5 pr-3 text-left text-[12px] ${
          active
            ? "bg-primary/10 text-primary"
            : "text-base-content hover:bg-base-200"
        }`}
      >
        {riskMeta !== null ? (
          <span
            aria-label={riskMeta.label}
            className="inline-block size-1.5 shrink-0 rounded-full"
            style={{ backgroundColor: riskMeta.ink }}
          />
        ) : hint !== undefined ? (
          <span aria-hidden className="inline-block size-1.5 shrink-0" />
        ) : null}
        <span
          aria-hidden
          className={`w-3 shrink-0 text-center font-mono font-semibold ${badge.cls}`}
        >
          {badge.letter}
        </span>
        <span className="min-w-0 flex-1 truncate font-mono">{node.name}</span>
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

function buildFileTitle(path: string, hint: FileOrderHint | undefined): string {
  if (hint === undefined) return path;
  const parts = [path];
  if (hint.risk !== null) parts.push(RISK_META[hint.risk].label);
  if (hint.reason !== null && hint.reason.length > 0) parts.push(hint.reason);
  return parts.join(" · ");
}
