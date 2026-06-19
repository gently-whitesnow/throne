import { ChevronRight } from "lucide-react";
import { useId, useState } from "react";

import type { PromptPartPreview } from "../model/types";

const ROLE_LABEL: Record<string, string> = {
  mandatory: "обязательная",
  default_on: "включена",
  default_off: "доступна"
};

interface PartFrameProps {
  part: PromptPartPreview;
  onToggle: (partId: string) => void;
  onTextChange: (partId: string, text: string) => void;
}

/**
 * Одна включённая часть в своей рамке: заголовок (имя · scope · роль) с тумблером
 * выключения и редактируемое тело. Правка тела — override только на эту сессию,
 * в prompt-part не персистится. Обязательная часть идёт без тумблера. Рамка
 * сворачиваемая, по умолчанию развёрнута — длинную часть можно свернуть.
 */
export function PartFrame({ part, onToggle, onTextChange }: PartFrameProps) {
  const [open, setOpen] = useState(true);
  const bodyId = useId();
  const mandatory = part.role === "mandatory";

  return (
    <section className="flex flex-col rounded-md border border-base-300 bg-base-100">
      <div className="flex items-center gap-2 px-2 py-1.5">
        <button
          type="button"
          onClick={() => {
            setOpen((value) => !value);
          }}
          aria-expanded={open}
          aria-controls={bodyId}
          className="flex min-w-0 flex-1 items-center gap-1.5 text-left"
        >
          <ChevronRight
            aria-hidden
            size={13}
            strokeWidth={2.5}
            className={`flex-shrink-0 text-base-content/40 transition-transform ${
              open ? "rotate-90" : ""
            }`}
          />
          <span className="truncate text-xs font-semibold text-base-content">
            {part.key}
          </span>
          <span className="flex-shrink-0 text-[11px] text-base-content/45">
            {part.scope}
          </span>
        </button>
        <span className="badge badge-ghost badge-sm flex-shrink-0">
          {ROLE_LABEL[part.role] ?? part.role}
        </span>
        {mandatory ? null : (
          <input
            type="checkbox"
            className="checkbox checkbox-xs flex-shrink-0"
            checked={part.selected}
            aria-label={`Часть ${part.key}`}
            onChange={() => {
              onToggle(part.part_id);
            }}
          />
        )}
      </div>
      {open ? (
        <div id={bodyId} className="px-2 pb-2">
          <textarea
            aria-label={`Текст части ${part.key}`}
            className="textarea textarea-bordered min-h-24 w-full resize-y font-mono text-xs leading-snug"
            value={part.text}
            onChange={(e) => {
              onTextChange(part.part_id, e.target.value);
            }}
          />
        </div>
      ) : null}
    </section>
  );
}

interface PartChipProps {
  part: PromptPartPreview;
  onEnable: (partId: string) => void;
}

/** Выключенная (доступная) часть компактным чипом — клик включает её. */
export function PartChip({ part, onEnable }: PartChipProps) {
  return (
    <button
      type="button"
      onClick={() => {
        onEnable(part.part_id);
      }}
      className="badge badge-outline badge-sm gap-1 border-dashed text-[11px] hover:border-primary hover:text-primary"
    >
      + {part.key}
    </button>
  );
}
