import {
  PROMPT_PART_MODES,
  PROMPT_PART_MODE_LABELS,
  type PromptPartListItem,
  type PromptPartMode
} from "@/entities/prompt-part";

import { includedCount } from "../model/block-buckets";

interface ModeRailProps {
  parts: PromptPartListItem[];
  selected: PromptPartMode;
  onSelect: (mode: PromptPartMode) => void;
}

/**
 * Left rail of run modes. The counter is «how many blocks actually ship in this
 * mode», so the operator picks a mode and immediately sees the prompt's size.
 */
export function ModeRail({ parts, selected, onSelect }: ModeRailProps) {
  return (
    <nav aria-label="Режимы" className="flex flex-col gap-2">
      <span className="px-1 text-[11px] font-bold uppercase tracking-wider text-base-content/50">
        Режим запуска
      </span>
      <div className="flex gap-1 overflow-x-auto pb-1 md:flex-col md:overflow-visible md:pb-0">
        {PROMPT_PART_MODES.map((mode) => {
          const active = mode === selected;
          const count = includedCount(parts, mode);
          return (
            <button
              key={mode}
              type="button"
              aria-current={active}
              onClick={() => {
                onSelect(mode);
              }}
              className={`flex flex-shrink-0 items-center justify-between gap-2 rounded-md px-3 py-2 text-left text-sm transition-[color,background-color,scale] active:scale-[0.98] md:flex-shrink ${
                active
                  ? "bg-primary/10 font-semibold text-primary"
                  : "text-base-content/70 hover:bg-base-200 hover:text-base-content"
              }`}
            >
              <span>{PROMPT_PART_MODE_LABELS[mode]}</span>
              <span
                className={`inline-flex h-[18px] min-w-[18px] items-center justify-center rounded-full px-1 text-[11px] font-semibold tabular-nums ${
                  active
                    ? "bg-primary/15 text-primary"
                    : "bg-base-200 text-base-content/60"
                }`}
                title="Блоков уходит в этом режиме"
              >
                {count}
              </span>
            </button>
          );
        })}
      </div>
    </nav>
  );
}
