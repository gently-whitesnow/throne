import { useCallback, useEffect, useMemo, useState } from "react";

import {
  getReviewFileLines,
  type ReviewFileLine
} from "@/entities/review-workspace";

interface LoadedRange {
  from: number;
  to: number;
}

interface LineState {
  lines: Map<number, string>;
  ranges: LoadedRange[];
  totalLines: number | null;
  loadingKey: string | null;
  error: string | null;
}

const EMPTY_STATE: LineState = {
  lines: new Map(),
  ranges: [],
  totalLines: null,
  loadingKey: null,
  error: null
};

export function useReviewFileLines(
  intentId: string,
  bindingId: string,
  sha: string,
  path: string
) {
  const [state, setState] = useState<LineState>(EMPTY_STATE);

  useEffect(() => {
    setState({ ...EMPTY_STATE, lines: new Map(), ranges: [] });
  }, [sha, path]);

  const loadRange = useCallback(
    async (key: string, from: number, to: number) => {
      if (covers(state.ranges, from, to)) return;
      setState((current) => ({ ...current, loadingKey: key, error: null }));
      try {
        const response = await getReviewFileLines(
          intentId,
          bindingId,
          sha,
          path,
          from,
          to
        );
        setState((current) => {
          const lines = new Map(current.lines);
          for (const line of response.lines) {
            lines.set(line.line, line.content);
          }
          return {
            lines,
            ranges: mergeRanges([
              ...current.ranges,
              { from, to: response.to < from ? from - 1 : response.to }
            ]),
            totalLines: response.total_lines,
            loadingKey: null,
            error: null
          };
        });
      } catch (error) {
        setState((current) => ({
          ...current,
          loadingKey: null,
          error:
            error instanceof Error
              ? error.message
              : "Не удалось загрузить строки"
        }));
      }
    },
    [bindingId, intentId, path, sha, state.ranges]
  );

  const getLines = useCallback(
    (from: number, to: number): ReviewFileLine[] => {
      const lines: ReviewFileLine[] = [];
      for (let line = from; line <= to; line += 1) {
        const content = state.lines.get(line);
        if (content !== undefined) {
          lines.push({ line, content });
        }
      }
      return lines;
    },
    [state.lines]
  );

  return useMemo(
    () => ({
      lines: state.lines,
      totalLines: state.totalLines,
      loadingKey: state.loadingKey,
      error: state.error,
      loadRange,
      getLines
    }),
    [
      getLines,
      loadRange,
      state.error,
      state.lines,
      state.loadingKey,
      state.totalLines
    ]
  );
}

function covers(
  ranges: readonly LoadedRange[],
  from: number,
  to: number
): boolean {
  return ranges.some((range) => range.from <= from && range.to >= to);
}

function mergeRanges(ranges: readonly LoadedRange[]): LoadedRange[] {
  const sorted = ranges
    .filter((range) => range.to >= range.from)
    .slice()
    .sort((a, b) => a.from - b.from);
  const merged: LoadedRange[] = [];
  for (const range of sorted) {
    const last = merged.at(-1);
    if (last === undefined || range.from > last.to + 1) {
      merged.push({ ...range });
    } else {
      last.to = Math.max(last.to, range.to);
    }
  }
  return merged;
}
