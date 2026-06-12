import { Lock } from "lucide-react";

import {
  PROMPT_PART_MODES,
  PROMPT_PART_MODE_LABELS,
  type PromptPartListItem
} from "@/entities/prompt-part";

import type { ProjectedInstruction } from "../model/composition";
import { RoleSelect } from "./RoleSelect";

function ScopeBadge({ scope }: { scope: string }) {
  const isSystem = scope === "system";
  return (
    <span
      className={`inline-flex h-[18px] items-center rounded-full px-2 text-[10px] font-bold uppercase tracking-wide ${
        isSystem
          ? "bg-base-300 text-base-content/70"
          : "bg-primary/10 text-primary"
      }`}
    >
      {scope}
    </span>
  );
}

function MustBadge() {
  return (
    <span className="inline-flex h-[18px] items-center gap-1 rounded-full bg-warning-soft px-2 text-[10px] font-bold uppercase tracking-wide text-warning">
      <Lock aria-hidden size={10} strokeWidth={2.5} />
      must
    </span>
  );
}

interface InstructionRowProps {
  instruction: ProjectedInstruction;
  onOpen: () => void;
}

/** Read-only row for a projected mandatory instruction (system or user). */
export function InstructionRow({ instruction, onOpen }: InstructionRowProps) {
  return (
    <li className="flex items-center gap-3 rounded-md border border-base-300 bg-base-100 px-3 py-2 hover:bg-base-200">
      <button
        type="button"
        className="flex flex-1 items-center gap-2 text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
        onClick={onOpen}
      >
        <ScopeBadge scope={instruction.scope} />
        <span className="font-mono text-[13px] font-semibold text-base-content">
          {instruction.kind}
        </span>
        {!instruction.present ? (
          <span className="text-xs text-warning">не создана</span>
        ) : null}
        <span className="ml-2 truncate text-xs text-base-content/60">
          {instruction.modes
            .map((m) =>
              m in PROMPT_PART_MODE_LABELS
                ? PROMPT_PART_MODE_LABELS[
                    m as keyof typeof PROMPT_PART_MODE_LABELS
                  ]
                : m
            )
            .join(", ")}
        </span>
      </button>
      <MustBadge />
    </li>
  );
}

interface OptionalRowProps {
  part: PromptPartListItem;
  onOpen: () => void;
  onRoleError: (message: string | null) => void;
}

/** Editable row for an optional user part with a role select per mode. */
export function OptionalRow({ part, onOpen, onRoleError }: OptionalRowProps) {
  return (
    <li className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100 px-3 py-2.5">
      <div className="flex items-start gap-3">
        <button
          type="button"
          className="flex flex-1 flex-col items-start gap-1 text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
          onClick={onOpen}
        >
          <span className="flex items-center gap-2">
            <ScopeBadge scope={part.scope} />
            <span className="font-mono text-[13px] font-semibold text-base-content">
              {part.key}
            </span>
          </span>
          {part.description ? (
            <span className="text-xs text-base-content/70">
              {part.description}
            </span>
          ) : null}
          <span className="line-clamp-2 font-mono text-xs text-base-content/50">
            {part.text_short}
          </span>
        </button>
      </div>
      <div className="flex flex-wrap gap-3 border-t border-base-200 pt-2">
        {PROMPT_PART_MODES.map((mode, index) => (
          <label key={mode} className="flex items-center gap-1.5">
            <span className="text-xs font-medium text-base-content/60">
              {PROMPT_PART_MODE_LABELS[mode]}
            </span>
            <RoleSelect
              part={part}
              mode={mode}
              orderForNew={index}
              onError={onRoleError}
            />
          </label>
        ))}
      </div>
    </li>
  );
}
