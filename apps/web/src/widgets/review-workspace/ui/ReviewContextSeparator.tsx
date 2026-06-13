import { ChevronsDown, ChevronsUp, Rows3 } from "lucide-react";
import type { ReactNode } from "react";

import type { DiffGap } from "@/entities/review-workspace";

interface ReviewContextSeparatorProps {
  gap: DiffGap;
  loadedLines: ReadonlySet<number>;
  loading: boolean;
  error: string | null;
  onLoad: (from: number, to: number) => void;
}

const CHUNK = 20;

export function ReviewContextSeparator({
  gap,
  loadedLines,
  loading,
  error,
  onLoad
}: ReviewContextSeparatorProps) {
  const bounds = hiddenBounds(gap, loadedLines);
  if (bounds === null) return null;
  const hiddenCount =
    bounds.to === null ? null : Math.max(0, bounds.to - bounds.from + 1);
  const loadAll = bounds.to ?? bounds.from + CHUNK - 1;
  const compact = hiddenCount !== null && hiddenCount <= CHUNK;

  return (
    <div className="grid grid-cols-[3rem_3rem_1.25rem_1fr] border-l-2 border-primary/30 bg-base-200 text-[11px] text-base-content/65">
      <span className="border-r border-base-300" />
      <span className="border-r border-base-300" />
      <span />
      <div className="flex min-h-8 flex-wrap items-center gap-1 px-2 py-1">
        {compact ? (
          <ExpandButton
            title="Раскрыть пропущенные строки"
            disabled={loading}
            onClick={() => {
              onLoad(bounds.from, loadAll);
            }}
          >
            <Rows3 size={14} />
          </ExpandButton>
        ) : (
          <>
            <ExpandButton
              title="Раскрыть сверху"
              disabled={loading}
              onClick={() => {
                onLoad(bounds.from, bounds.from + CHUNK - 1);
              }}
            >
              <ChevronsDown size={14} />
            </ExpandButton>
            {bounds.to !== null ? (
              <ExpandButton
                title="Раскрыть снизу"
                disabled={loading}
                onClick={() => {
                  onLoad(
                    Math.max(bounds.from, bounds.to - CHUNK + 1),
                    bounds.to
                  );
                }}
              >
                <ChevronsUp size={14} />
              </ExpandButton>
            ) : null}
            <ExpandButton
              title="Раскрыть весь промежуток"
              disabled={loading || bounds.to === null}
              onClick={() => {
                onLoad(bounds.from, loadAll);
              }}
            >
              <Rows3 size={14} />
            </ExpandButton>
          </>
        )}
        {error !== null ? (
          <span className="ml-2 text-error/80">Контекст недоступен</span>
        ) : null}
      </div>
    </div>
  );
}

function ExpandButton({
  title,
  disabled,
  onClick,
  children
}: {
  title: string;
  disabled: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      title={title}
      aria-label={title}
      disabled={disabled}
      onClick={onClick}
      className="inline-flex h-6 w-7 items-center justify-center rounded border border-base-300 bg-base-100 text-base-content/70 hover:border-primary hover:text-primary disabled:cursor-wait disabled:opacity-50"
    >
      {children}
    </button>
  );
}

function hiddenBounds(gap: DiffGap, loadedLines: ReadonlySet<number>) {
  let from = gap.from;
  while (loadedLines.has(from)) from += 1;
  if (gap.to === null) return { from, to: null };

  let to = gap.to;
  while (to >= from && loadedLines.has(to)) to -= 1;
  return from <= to ? { from, to } : null;
}
