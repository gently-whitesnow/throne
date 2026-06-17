import { useMemo } from "react";

import {
  buildSideBySideDiff,
  type DiffCell,
  type DiffRow
} from "../lib/build-side-by-side-diff";

export interface SideBySideDiffProps {
  leftText: string;
  rightText: string;
  leftLabel: string;
  rightLabel: string;
  maxHeightClass?: string;
}

export function SideBySideDiff({
  leftText,
  rightText,
  leftLabel,
  rightLabel,
  maxHeightClass = "max-h-[40vh]"
}: SideBySideDiffProps) {
  const rows = useMemo(
    () => buildSideBySideDiff(leftText, rightText),
    [leftText, rightText]
  );

  return (
    <div className="overflow-hidden rounded-md border border-base-300 bg-base-100">
      <header className="grid grid-cols-1 border-b border-base-300 text-xs font-semibold uppercase tracking-wide text-base-content/60 md:grid-cols-2">
        <span className="px-3 py-2">{leftLabel}</span>
        <span className="border-t border-base-300 px-3 py-2 md:border-l md:border-t-0">
          {rightLabel}
        </span>
      </header>
      <div className={`${maxHeightClass} overflow-auto`}>
        {rows.length === 0 ? (
          <p className="m-0 px-3 py-2 text-xs text-base-content/60">(пусто)</p>
        ) : (
          rows.map((row, idx) => <DiffRowView key={idx} row={row} />)
        )}
      </div>
    </div>
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
