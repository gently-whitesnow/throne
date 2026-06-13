import { FileText } from "lucide-react";
import { Fragment, useEffect, useMemo, useRef, useState } from "react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import {
  detectLanguage,
  fileLinesToContextRows,
  findDiffGaps,
  mergeFullFileLinesWithDiff,
  parseUnifiedDiff,
  type DiffGap,
  type DiffRow,
  type PullRequestDiffFile,
  type ReviewFileLine,
  type ReviewCommentAnchorShas
} from "@/entities/review-workspace";

import { indexInlineComments } from "../lib/match-inline-comments";
import { useReviewFileLines } from "../model/use-review-file-lines";
import type { ScrollTarget } from "../model/use-review-workspace";
import { ReviewContextSeparator } from "./ReviewContextSeparator";
import { ReviewDiffRow } from "./ReviewDiffRow";
import type { CommentActions } from "./ReviewCommentCard";
import type { ComposerAnchor } from "./ReviewInlineComposer";

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

const FULL_FILE_TO_LINE = 2_147_483_647;

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
  const [fullFile, setFullFile] = useState(false);
  const fileLines = useReviewFileLines(
    intentId,
    bindingId,
    shas.commit_sha,
    file.path
  );
  const commentsByAnchor = useMemo(
    () => indexInlineComments(file, comments),
    [file, comments]
  );
  const gaps = useMemo(
    () => findDiffGaps(hunks, fileLines.totalLines),
    [hunks, fileLines.totalLines]
  );
  const fullFileRows = useMemo(() => {
    if (!fullFile || fileLines.totalLines === null) return [];
    return mergeFullFileLinesWithDiff(
      fileLines.getLines(1, fileLines.totalLines),
      hunks
    );
  }, [fileLines, fullFile, hunks]);

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
      <div className="flex items-center justify-end border-b border-base-300 bg-base-100 px-2 py-1">
        <button
          type="button"
          className="inline-flex h-7 items-center gap-1 rounded border border-base-300 px-2 text-[11px] text-base-content/70 hover:border-primary hover:text-primary disabled:cursor-wait disabled:opacity-50"
          disabled={fileLines.loadingKey === "full-file"}
          onClick={() => {
            const next = !fullFile;
            setFullFile(next);
            if (next) {
              void fileLines.loadRange("full-file", 1, FULL_FILE_TO_LINE);
            }
          }}
        >
          <FileText size={13} />
          {fullFile ? "Патч" : "Весь файл"}
        </button>
      </div>
      {fullFile && fileLines.totalLines !== null ? (
        <div className="border-b border-base-300 last:border-b-0">
          {fullFileRows.map((row, index) =>
            renderRow(row, `full-${String(index)}`)
          )}
        </div>
      ) : (
        hunks.map((hunk, hi) => (
          <div key={hi} className="border-b border-base-300 last:border-b-0">
            {renderGap(gaps.find((gap) => gap.id === gapIdBefore(hi)))}
            <div className="bg-base-200 px-3 py-1 text-[11px] text-base-content/60">
              {hunk.header}
            </div>
            {hunk.rows.map((row, ri) =>
              renderRow(row, `hunk-${String(hi)}-${String(ri)}`)
            )}
            {hi === hunks.length - 1
              ? renderGap(gaps.find((gap) => gap.id === "bottom"))
              : null}
          </div>
        ))
      )}
    </div>
  );

  function renderGap(gap: DiffGap | undefined) {
    if (gap === undefined) return null;
    const to = gap.to ?? gap.from - 1;
    const topRows =
      gap.to === null ? fileLines.getLines(gap.from, to) : leadingLoaded(gap);
    const bottomRows =
      gap.to === null
        ? []
        : trailingLoaded(gap, topRows.at(-1)?.line ?? gap.from - 1);
    const loaded = new Set(
      [...topRows, ...bottomRows].map((line) => line.line)
    );
    const loadedRows = [
      ...fileLinesToContextRows(topRows, gap.oldLineDelta),
      ...fileLinesToContextRows(bottomRows, gap.oldLineDelta)
    ];
    return (
      <Fragment key={gap.id}>
        {loadedRows.map((row, index) =>
          renderRow(row, `${gap.id}-${String(index)}`)
        )}
        <ReviewContextSeparator
          gap={gap}
          loadedLines={loaded}
          loading={fileLines.loadingKey === gap.id}
          error={fileLines.error}
          onLoad={(from, toLine) => {
            void fileLines.loadRange(gap.id, from, toLine);
          }}
        />
      </Fragment>
    );
  }

  function renderRow(row: DiffRow, rowKey: string) {
    return (
      <ReviewDiffRow
        key={rowKey}
        row={row}
        file={file}
        language={language}
        composerKey={composerKey}
        target={target}
        setTarget={setTarget}
        commentsByAnchor={commentsByAnchor}
        flashKey={flashKey}
        rowRefs={rowRefs}
        commentActions={commentActions}
        intentId={intentId}
        bindingId={bindingId}
        shas={shas}
        onSubmitted={onSubmitted}
      />
    );
  }

  function leadingLoaded(gap: DiffGap) {
    const lines: ReviewFileLine[] = [];
    for (let line = gap.from; gap.to !== null && line <= gap.to; line += 1) {
      const content = fileLines.lines.get(line);
      if (content === undefined) break;
      lines.push({ line, content });
    }
    return lines;
  }

  function trailingLoaded(gap: DiffGap, topEnd: number) {
    const lines: ReviewFileLine[] = [];
    if (gap.to === null) return lines;
    for (let line = gap.to; line > topEnd; line -= 1) {
      const content = fileLines.lines.get(line);
      if (content === undefined) break;
      lines.unshift({ line, content });
    }
    return lines;
  }
}

function gapIdBefore(hunkIndex: number): string {
  return hunkIndex === 0
    ? "top"
    : `between-${String(hunkIndex - 1)}-${String(hunkIndex)}`;
}
