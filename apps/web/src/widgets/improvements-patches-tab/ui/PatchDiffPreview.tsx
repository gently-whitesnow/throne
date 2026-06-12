import { useMemo } from "react";

import type {
  PromptPartPatchDetail,
  PromptPartPatchStatus
} from "@/entities/prompt-part-patch";

import {
  buildSideBySideDiff,
  type DiffCell,
  type DiffRow
} from "../lib/build-side-by-side-diff";

/**
 * Patch preview with line-level red/green diff against the prompt part's
 * **base** version — i.e. the text the agent saw when proposing the patch.
 * The right side is status-aware:
 *
 *   • proposed                → `patch_text` (proposed)
 *   • applied / applied_edited → `applied_text` (what was actually written)
 *   • rejected / superseded   → `patch_text` (what was proposed and discarded)
 *
 * For `proposed` patches with `base_version_matches_current=false` (someone
 * updated the prompt part after the patch was proposed) we still diff
 * `base → proposed` cleanly and show a separate warning about current drift,
 * instead of falling back to illegible "plain text in both panes".
 */
export function PatchDiffPreview({
  detail
}: {
  detail: PromptPartPatchDetail;
}) {
  const view = useMemo(() => resolveDiffView(detail), [detail]);
  const rows = useMemo(
    () => buildSideBySideDiff(view.leftText, view.rightText),
    [view.leftText, view.rightText]
  );
  const showStaleWarning =
    detail.patch.status === "proposed" && !detail.base_version_matches_current;

  return (
    <div className="flex flex-col gap-2">
      {showStaleWarning ? (
        <p className="m-0 rounded border border-warning/30 bg-warning/10 px-3 py-2 text-xs leading-relaxed text-warning-content/90">
          Патч предложен против v{String(detail.patch.base_version)}, текущая
          версия части — v{String(detail.current_part_version)}. Дифф ниже
          сравнивает base → proposed; apply без правок повлечёт 409
          needs_rebase.
        </p>
      ) : null}
      <div className="overflow-hidden rounded-md border border-base-300 bg-base-100">
        <PreviewHeader
          leftLabel={view.leftLabel}
          rightLabel={view.rightLabel}
        />
        <div className="max-h-[40vh] overflow-auto">
          {rows.length === 0 ? (
            <p className="m-0 px-3 py-2 text-xs text-base-content/60">
              (пусто)
            </p>
          ) : (
            rows.map((row, idx) => <DiffRowView key={idx} row={row} />)
          )}
        </div>
      </div>
    </div>
  );
}

interface DiffView {
  leftText: string;
  rightText: string;
  leftLabel: string;
  rightLabel: string;
}

function resolveDiffView(detail: PromptPartPatchDetail): DiffView {
  const baseVersion = detail.patch.base_version;
  const leftLabel = `Base v${String(baseVersion)}`;
  const leftText = detail.base_part_text;
  const status: PromptPartPatchStatus = detail.patch.status;

  if (status === "applied" || status === "applied_edited") {
    const appliedVersion = detail.patch.applied_version;
    const rightLabel = `Applied${
      appliedVersion ? ` v${String(appliedVersion)}` : ""
    }${status === "applied_edited" ? " (edited)" : ""}`;
    return {
      leftText,
      rightText: detail.patch.applied_text ?? detail.patch.patch_text,
      leftLabel,
      rightLabel
    };
  }

  if (status === "rejected") {
    return {
      leftText,
      rightText: detail.patch.patch_text,
      leftLabel,
      rightLabel: "Rejected (proposed)"
    };
  }

  if (status === "superseded") {
    return {
      leftText,
      rightText: detail.patch.patch_text,
      leftLabel,
      rightLabel: "Superseded (proposed)"
    };
  }

  // proposed
  return {
    leftText,
    rightText: detail.patch.patch_text,
    leftLabel,
    rightLabel: "Proposed"
  };
}

function PreviewHeader({
  leftLabel,
  rightLabel
}: {
  leftLabel: string;
  rightLabel: string;
}) {
  return (
    <header className="grid grid-cols-1 border-b border-base-300 text-xs font-semibold uppercase tracking-wide text-base-content/60 md:grid-cols-2">
      <span className="px-3 py-2">{leftLabel}</span>
      <span className="border-t border-base-300 px-3 py-2 md:border-l md:border-t-0">
        {rightLabel}
      </span>
    </header>
  );
}

function DiffRowView({ row }: { row: DiffRow }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2">
      <DiffCellView cell={row.left} side="left" />
      <DiffCellView cell={row.right} side="right" />
    </div>
  );
}

function DiffCellView({
  cell,
  side
}: {
  cell: DiffCell;
  side: "left" | "right";
}) {
  const tone =
    cell.kind === "removed"
      ? "bg-error/10"
      : cell.kind === "added"
        ? "bg-success/10"
        : "";
  const borderLeft = side === "right" ? "md:border-l md:border-base-300" : "";
  const text = cell.kind === "empty" || cell.text === "" ? " " : cell.text;
  return (
    <pre
      className={`m-0 whitespace-pre-wrap break-words px-3 py-0.5 font-mono text-xs leading-relaxed text-base-content/85 ${tone} ${borderLeft}`}
    >
      {text}
    </pre>
  );
}
