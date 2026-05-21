import { useMemo } from "react";

import type { InstructionPatchDetail } from "@/entities/instruction-patch";

import {
  buildSideBySideDiff,
  type DiffCell,
  type DiffRow
} from "../lib/build-side-by-side-diff";

export function PatchDiffPreview({
  detail
}: {
  detail: InstructionPatchDetail;
}) {
  if (!detail.base_version_matches_current) {
    return <StaleBasePreview detail={detail} />;
  }
  return <DiffPreview detail={detail} />;
}

function DiffPreview({ detail }: { detail: InstructionPatchDetail }) {
  const rows = useMemo(
    () =>
      buildSideBySideDiff(
        detail.current_instruction_text,
        detail.patch.patch_text
      ),
    [detail.current_instruction_text, detail.patch.patch_text]
  );

  return (
    <div className="overflow-hidden rounded-md border border-base-300 bg-base-100">
      <PreviewHeader
        currentVersion={detail.current_instruction_version}
        rightTitle="Proposed"
      />
      <div className="max-h-[40vh] overflow-auto">
        {rows.length === 0 ? (
          <p className="m-0 px-3 py-2 text-xs text-base-content/60">(пусто)</p>
        ) : (
          rows.map((row, idx) => <DiffRowView key={idx} row={row} />)
        )}
      </div>
    </div>
  );
}

function StaleBasePreview({ detail }: { detail: InstructionPatchDetail }) {
  return (
    <div className="flex flex-col gap-2">
      <p className="m-0 rounded border border-warning/30 bg-warning/10 px-3 py-2 text-xs leading-relaxed text-warning-content/90">
        Патч против v{String(detail.patch.base_instruction_version)}, текущая
        инструкция — v{String(detail.current_instruction_version)}. Дифф не
        подсвечивается — нужен rebase, иначе сравнение неточно.
      </p>
      <div className="overflow-hidden rounded-md border border-base-300 bg-base-100">
        <PreviewHeader
          currentVersion={detail.current_instruction_version}
          rightTitle={`Proposed (base v${String(detail.patch.base_instruction_version)})`}
        />
        <div className="grid max-h-[40vh] grid-cols-1 overflow-auto md:grid-cols-2">
          <PlainPane text={detail.current_instruction_text} />
          <PlainPane
            text={detail.patch.patch_text}
            className="border-t border-base-300 md:border-l md:border-t-0"
          />
        </div>
      </div>
    </div>
  );
}

function PreviewHeader({
  currentVersion,
  rightTitle
}: {
  currentVersion: number;
  rightTitle: string;
}) {
  return (
    <header className="grid grid-cols-1 border-b border-base-300 text-xs font-semibold uppercase tracking-wide text-base-content/60 md:grid-cols-2">
      <span className="px-3 py-2">Current (v{String(currentVersion)})</span>
      <span className="border-t border-base-300 px-3 py-2 md:border-l md:border-t-0">
        {rightTitle}
      </span>
    </header>
  );
}

function PlainPane({
  text,
  className = ""
}: {
  text: string;
  className?: string;
}) {
  return (
    <pre
      className={`m-0 whitespace-pre-wrap break-words px-3 py-2 font-mono text-xs leading-relaxed text-base-content/85 ${className}`}
    >
      {text || "(пусто)"}
    </pre>
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
