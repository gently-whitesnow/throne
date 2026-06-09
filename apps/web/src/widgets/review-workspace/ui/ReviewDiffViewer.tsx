import { MessageSquarePlus } from "lucide-react";
import { Fragment, useEffect, useMemo, useRef, useState } from "react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import {
  detectLanguage,
  highlightLine,
  parseUnifiedDiff,
  type DiffRow,
  type PullRequestDiffFile,
  type ReviewCommentAnchorShas
} from "@/entities/review-workspace";

import {
  indexInlineComments,
  rowAnchorKey
} from "../lib/match-inline-comments";
import type { ScrollTarget } from "../model/use-review-workspace";
import type { CommentActions } from "./ReviewCommentCard";
import {
  ReviewInlineComposer,
  type ComposerAnchor
} from "./ReviewInlineComposer";
import { ReviewInlineThread } from "./ReviewInlineThread";

interface ReviewDiffViewerProps {
  file: PullRequestDiffFile;
  shas: ReviewCommentAnchorShas;
  intentId: string;
  bindingId: string;
  comments: PullRequestComment[];
  commentActions: CommentActions;
  scrollTarget: ScrollTarget | null;
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

const ROW_STYLE: Record<DiffRow["kind"], string> = {
  context: "bg-base-100 border-l-2 border-transparent",
  add: "bg-success/15 border-l-2 border-success/70",
  del: "bg-error/15 border-l-2 border-error/70"
};

const GUTTER_STYLE: Record<DiffRow["kind"], string> = {
  context: "text-base-content/40",
  add: "bg-success/10 text-success/80",
  del: "bg-error/10 text-error/80"
};

const SIGN_STYLE: Record<DiffRow["kind"], string> = {
  context: "text-base-content/30",
  add: "text-success",
  del: "text-error"
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
  comments,
  commentActions,
  scrollTarget,
  onSubmitted
}: ReviewDiffViewerProps) {
  const hunks = useMemo(() => parseUnifiedDiff(file.patch), [file.patch]);
  const language = useMemo(() => detectLanguage(file.path), [file.path]);
  const [target, setTarget] = useState<ComposerAnchor | null>(null);
  const commentsByAnchor = useMemo(
    () => indexInlineComments(file, comments),
    [file, comments]
  );

  // Scroll the requested row into view and briefly highlight it. The nonce makes
  // the effect re-run even when the same row is requested twice in a row.
  const rowRefs = useRef(new Map<string, HTMLDivElement>());
  const [flashKey, setFlashKey] = useState<string | null>(null);
  useEffect(() => {
    if (scrollTarget?.path !== file.path) return;
    const key = `${scrollTarget.side}:${String(scrollTarget.line)}`;
    const node = rowRefs.current.get(key);
    if (node === undefined) return;
    // jsdom (tests) lacks scrollIntoView; the highlight still applies.
    if (typeof node.scrollIntoView === "function") {
      node.scrollIntoView({ block: "center" });
    }
    setFlashKey(key);
    const timer = window.setTimeout(() => {
      setFlashKey(null);
    }, 1600);
    return () => {
      window.clearTimeout(timer);
    };
  }, [scrollTarget, file.path]);

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
    <div className="diff-hl font-mono text-[12px] leading-[1.5]">
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
            const anchorKey = rowAnchorKey(row);
            const rowComments =
              anchorKey !== null
                ? (commentsByAnchor.get(anchorKey) ?? null)
                : null;
            const isFlashing = anchorKey !== null && anchorKey === flashKey;
            return (
              <Fragment key={rowKey}>
                <div
                  ref={
                    anchorKey !== null
                      ? (node) => {
                          if (node !== null) {
                            rowRefs.current.set(anchorKey, node);
                          } else {
                            rowRefs.current.delete(anchorKey);
                          }
                        }
                      : undefined
                  }
                  className={`group grid grid-cols-[3rem_3rem_1.25rem_1fr] ${ROW_STYLE[row.kind]} ${
                    isFlashing
                      ? "outline outline-2 -outline-offset-2 outline-primary"
                      : ""
                  }`}
                >
                  <Gutter value={row.oldLine} kind={row.kind} />
                  <Gutter value={row.newLine} kind={row.kind} />
                  <span
                    className={`select-none text-center ${SIGN_STYLE[row.kind]}`}
                  >
                    {SIGN[row.kind]}
                  </span>
                  <span className="flex items-start gap-1 pr-2">
                    <code
                      className="min-w-0 flex-1 whitespace-pre-wrap break-words"
                      dangerouslySetInnerHTML={{
                        __html: highlightLine(row.content, language)
                      }}
                    />
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
                {rowComments !== null ? (
                  <ReviewInlineThread
                    comments={rowComments}
                    actions={commentActions}
                  />
                ) : null}
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

function Gutter({
  value,
  kind
}: {
  value: number | null;
  kind: DiffRow["kind"];
}) {
  return (
    <span
      className={`select-none border-r border-base-300 px-2 text-right tabular-nums ${GUTTER_STYLE[kind]}`}
    >
      {value ?? ""}
    </span>
  );
}
