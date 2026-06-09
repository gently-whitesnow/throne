import { MessageSquarePlus } from "lucide-react";
import { Fragment, useMemo, useState } from "react";

import {
  parseUnifiedDiff,
  type DiffRow,
  type PullRequestDiffFile,
  type ReviewCommentAnchorShas
} from "@/entities/review-workspace";

import {
  ReviewInlineComposer,
  type ComposerAnchor
} from "./ReviewInlineComposer";

interface ReviewDiffViewerProps {
  file: PullRequestDiffFile;
  shas: ReviewCommentAnchorShas;
  intentId: string;
  bindingId: string;
  onSubmitted: () => void;
}

function anchorFromRow(
  file: PullRequestDiffFile,
  row: DiffRow
): ComposerAnchor | null {
  if (row.kind === "del") {
    if (row.oldLine === null) return null;
    return {
      path: file.path,
      previousPath: file.previous_path ?? null,
      side: "left",
      line: row.oldLine
    };
  }
  if (row.newLine === null) return null;
  return {
    path: file.path,
    previousPath: file.previous_path ?? null,
    side: "right",
    line: row.newLine
  };
}

const ROW_BG: Record<DiffRow["kind"], string> = {
  context: "bg-base-100",
  add: "bg-success-soft/50",
  del: "bg-error-soft/50"
};

const SIGN: Record<DiffRow["kind"], string> = {
  context: " ",
  add: "+",
  del: "-"
};

export function ReviewDiffViewer({
  file,
  shas,
  intentId,
  bindingId,
  onSubmitted
}: ReviewDiffViewerProps) {
  const hunks = useMemo(() => parseUnifiedDiff(file.patch), [file.patch]);
  const [target, setTarget] = useState<ComposerAnchor | null>(null);

  if (hunks.length === 0) {
    return (
      <p className="m-0 px-4 py-6 text-xs text-base-content/60">
        Diff недоступен для этого файла (бинарный, переименование без изменений
        или слишком большой).
      </p>
    );
  }

  const composerKey =
    target !== null ? `${target.side}:${String(target.line)}` : null;

  return (
    <div className="font-mono text-[12px] leading-[1.5]">
      {hunks.map((hunk, hi) => (
        <div key={hi} className="border-b border-base-300 last:border-b-0">
          <div className="bg-base-200 px-3 py-1 text-[11px] text-base-content/60">
            {hunk.header}
          </div>
          {hunk.rows.map((row, ri) => {
            const anchor = anchorFromRow(file, row);
            const rowKey = `${row.kind}-${String(row.oldLine)}-${String(
              row.newLine
            )}-${String(ri)}`;
            const isTarget =
              anchor !== null &&
              composerKey === `${anchor.side}:${String(anchor.line)}`;
            return (
              <Fragment key={rowKey}>
                <div
                  className={`group grid grid-cols-[3rem_3rem_1.25rem_1fr] ${ROW_BG[row.kind]}`}
                >
                  <Gutter value={row.oldLine} />
                  <Gutter value={row.newLine} />
                  <span className="select-none text-center text-base-content/50">
                    {SIGN[row.kind]}
                  </span>
                  <span className="flex items-start gap-1 whitespace-pre-wrap break-words pr-2">
                    <span className="min-w-0 flex-1">{row.content}</span>
                    {anchor !== null ? (
                      <button
                        type="button"
                        aria-label="Добавить inline-комментарий"
                        onClick={() => {
                          setTarget(isTarget ? null : anchor);
                        }}
                        className="invisible mt-0.5 shrink-0 text-base-content/40 hover:text-primary group-hover:visible"
                      >
                        <MessageSquarePlus size={13} strokeWidth={2} />
                      </button>
                    ) : null}
                  </span>
                </div>
                {isTarget && target !== null ? (
                  <ReviewInlineComposer
                    intentId={intentId}
                    bindingId={bindingId}
                    anchor={target}
                    shas={shas}
                    onCancel={() => {
                      setTarget(null);
                    }}
                    onSubmitted={() => {
                      setTarget(null);
                      onSubmitted();
                    }}
                  />
                ) : null}
              </Fragment>
            );
          })}
        </div>
      ))}
    </div>
  );
}

function Gutter({ value }: { value: number | null }) {
  return (
    <span className="select-none border-r border-base-300 px-2 text-right text-base-content/40 tabular-nums">
      {value ?? ""}
    </span>
  );
}
