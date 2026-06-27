import type { ReactNode } from "react";

import { promptRegionAccent, type PromptRegion } from "@/shared/lib";

interface SlotSectionProps {
  /** Anchor id for the slot rail jump links. */
  id: string;
  /** Position in the assembly (1–4), shown as a numbered marker. */
  index: number;
  title: string;
  /** Where the slot's content comes from («источник»). */
  source: string;
  /** Where the operator changes it («где менять»). */
  whereToChange?: string;
  /** Inline marker after the title (count badge, «скоро»). */
  marker?: ReactNode;
  /** Subdue the frame for not-yet-available slots. */
  muted?: boolean;
  /** Tint the whole slot as a system/user prompt zone. */
  region?: PromptRegion;
  children: ReactNode;
}

/**
 * One slot of the «что уйдёт агенту» assembly. The numbered header + источник /
 * где менять lines give a newcomer the mental model without external docs:
 * read top-to-bottom and each block says what it is and where to edit it.
 */
export function SlotSection({
  id,
  index,
  title,
  source,
  whereToChange,
  marker,
  muted = false,
  region,
  children
}: SlotSectionProps) {
  const accent = region ? promptRegionAccent(region) : null;
  return (
    <section
      id={id}
      aria-label={title}
      className={`flex scroll-mt-6 flex-col gap-4 ${
        accent ? "border-l-[3px] py-1 pl-5" : ""
      } ${muted ? "opacity-70" : ""}`}
      style={accent ? { borderLeftColor: accent.stripe } : undefined}
    >
      <header className="flex items-start gap-3 border-b border-base-300 pb-3">
        <span
          aria-hidden
          className="inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-sm font-bold tabular-nums text-primary"
        >
          {index}
        </span>
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="m-0 text-lg font-semibold leading-tight tracking-tight">
              {title}
            </h2>
            {marker}
          </div>
          <p className="m-0 text-xs leading-relaxed text-base-content/60">
            <span className="font-semibold text-base-content/70">
              источник:
            </span>{" "}
            {source}
          </p>
          {whereToChange ? (
            <p className="m-0 text-xs leading-relaxed text-base-content/50">
              <span className="font-semibold text-base-content/60">
                где менять:
              </span>{" "}
              {whereToChange}
            </p>
          ) : null}
        </div>
      </header>
      <div>{children}</div>
    </section>
  );
}

/** «скоро» marker for slots whose mechanism is not built yet. */
export function SoonChip() {
  return (
    <span className="inline-flex h-[20px] items-center rounded-full bg-base-200 px-2 text-[11px] font-semibold uppercase tracking-wide text-base-content/50">
      скоро
    </span>
  );
}

/** Neutral count marker («N») for a slot title. */
export function SlotCountMarker({ value }: { value: number }) {
  return (
    <span className="inline-flex h-[20px] items-center gap-1 rounded-full bg-warning-soft px-2 text-[11px] font-semibold tabular-nums text-warning">
      {value} на ревью
    </span>
  );
}
