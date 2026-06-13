import type { DiffHunk, DiffRow } from "./parse-unified-diff";

export interface ReviewFileLine {
  line: number;
  content: string;
}

export interface DiffGap {
  id: string;
  from: number;
  to: number | null;
  oldLineDelta: number;
}

export function findDiffGaps(
  hunks: DiffHunk[],
  totalLines: number | null
): DiffGap[] {
  if (hunks.length === 0) return [];
  const gaps: DiffGap[] = [];
  const first = hunks[0];
  if (first.newStart > 1) {
    gaps.push({
      id: "top",
      from: 1,
      to: first.newStart - 1,
      oldLineDelta: first.oldStart - first.newStart
    });
  }

  for (let index = 1; index < hunks.length; index += 1) {
    const prev = hunks[index - 1];
    const next = hunks[index];
    const from = prev.newStart + prev.newLines;
    const to = next.newStart - 1;
    if (from <= to) {
      gaps.push({
        id: `between-${String(index - 1)}-${String(index)}`,
        from,
        to,
        oldLineDelta:
          prev.oldStart + prev.oldLines - (prev.newStart + prev.newLines)
      });
    }
  }

  const last = hunks[hunks.length - 1];
  const bottomFrom = last.newStart + last.newLines;
  if (bottomFrom >= 1 && (totalLines === null || bottomFrom <= totalLines)) {
    gaps.push({
      id: "bottom",
      from: bottomFrom,
      to: totalLines,
      oldLineDelta:
        last.oldStart + last.oldLines - (last.newStart + last.newLines)
    });
  }
  return gaps;
}

export function fileLinesToContextRows(
  lines: readonly ReviewFileLine[],
  oldLineDelta: number
): DiffRow[] {
  return lines.map((line) => ({
    kind: "context",
    oldLine: line.line + oldLineDelta,
    newLine: line.line,
    content: line.content
  }));
}

export function mergeFullFileLinesWithDiff(
  lines: readonly ReviewFileLine[],
  hunks: readonly DiffHunk[]
): DiffRow[] {
  const changedByNewLine = new Map<number, DiffRow>();
  const deletedBeforeNewLine = new Map<number, DiffRow[]>();
  const contextOldByNewLine = new Map<number, number>();

  for (const hunk of hunks) {
    for (let index = 0; index < hunk.rows.length; index += 1) {
      const row = hunk.rows[index];
      if (row.kind === "add" && row.newLine !== null) {
        changedByNewLine.set(row.newLine, row);
      } else if (
        row.kind === "context" &&
        row.newLine !== null &&
        row.oldLine !== null
      ) {
        contextOldByNewLine.set(row.newLine, row.oldLine);
      } else if (row.kind === "del") {
        const nextNew =
          nextNewLine(hunk.rows, index) ?? hunk.newStart + hunk.newLines;
        const bucket = deletedBeforeNewLine.get(nextNew) ?? [];
        bucket.push(row);
        deletedBeforeNewLine.set(nextNew, bucket);
      }
    }
  }

  const result: DiffRow[] = [];
  for (const line of lines) {
    result.push(...(deletedBeforeNewLine.get(line.line) ?? []));
    result.push(
      changedByNewLine.get(line.line) ?? {
        kind: "context",
        oldLine:
          contextOldByNewLine.get(line.line) ??
          oldLineForNewLine(hunks, line.line),
        newLine: line.line,
        content: line.content
      }
    );
  }
  result.push(...(deletedBeforeNewLine.get(lines.length + 1) ?? []));
  return result;
}

function nextNewLine(
  rows: readonly DiffRow[],
  startIndex: number
): number | null {
  for (let index = startIndex + 1; index < rows.length; index += 1) {
    const line = rows[index].newLine;
    if (line !== null) return line;
  }
  return null;
}

function oldLineForNewLine(
  hunks: readonly DiffHunk[],
  newLine: number
): number {
  let delta = 0;
  for (const hunk of hunks) {
    if (newLine < hunk.newStart) return newLine + delta;
    delta = hunk.oldStart + hunk.oldLines - (hunk.newStart + hunk.newLines);
    if (newLine <= hunk.newStart + hunk.newLines - 1) return newLine + delta;
  }
  return newLine + delta;
}
