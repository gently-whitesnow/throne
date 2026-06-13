import { MessageSquarePlus } from "lucide-react";
import {
  Fragment,
  type Dispatch,
  type RefObject,
  type SetStateAction
} from "react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import {
  highlightLine,
  type DiffRow,
  type PullRequestDiffFile,
  type ReviewCommentAnchorShas
} from "@/entities/review-workspace";

import { rowAnchorKey } from "../lib/match-inline-comments";
import type { CommentActions } from "./ReviewCommentCard";
import {
  ReviewInlineComposer,
  type ComposerAnchor
} from "./ReviewInlineComposer";
import { ReviewInlineThread } from "./ReviewInlineThread";

interface ReviewDiffRowProps {
  row: DiffRow;
  file: PullRequestDiffFile;
  language: string | null;
  composerKey: string | null;
  target: ComposerAnchor | null;
  setTarget: Dispatch<SetStateAction<ComposerAnchor | null>>;
  commentsByAnchor: Map<string, PullRequestComment[]>;
  flashKey: string | null;
  rowRefs: RefObject<Map<string, HTMLDivElement>>;
  commentActions: CommentActions;
  intentId: string;
  bindingId: string;
  shas: ReviewCommentAnchorShas;
  onSubmitted: () => void;
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

export function ReviewDiffRow({
  row,
  file,
  language,
  composerKey,
  target,
  setTarget,
  commentsByAnchor,
  flashKey,
  rowRefs,
  commentActions,
  intentId,
  bindingId,
  shas,
  onSubmitted
}: ReviewDiffRowProps) {
  const anchor = anchorFromRow(file, row);
  const isTarget =
    anchor !== null && composerKey === `${anchor.side}:${String(anchor.line)}`;
  const anchorKey = rowAnchorKey(row);
  const rowComments =
    anchorKey !== null ? (commentsByAnchor.get(anchorKey) ?? null) : null;
  const isFlashing = anchorKey !== null && anchorKey === flashKey;

  return (
    <Fragment>
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
        <span className={`select-none text-center ${SIGN_STYLE[row.kind]}`}>
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
        <ReviewInlineThread comments={rowComments} actions={commentActions} />
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
      line: row.oldLine,
      oldLine: row.oldLine,
      newLine: null
    };
  }
  if (row.newLine === null) return null;
  return {
    path: file.path,
    previousPath: file.previous_path ?? null,
    side: "right",
    line: row.newLine,
    oldLine: row.kind === "context" ? row.oldLine : null,
    newLine: row.newLine
  };
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
