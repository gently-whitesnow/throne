import { Check, Lock } from "lucide-react";
import { useState } from "react";

import {
  PROMPT_PART_MODES,
  type PromptPartListItem,
  type PromptPartMode
} from "@/entities/prompt-part";

import type { EffectivePart } from "../model/composition";
import { RoleSelect } from "./RoleSelect";

interface ModeCompositionRowProps {
  part: EffectivePart;
  mode: PromptPartMode;
  optionalParts: PromptPartListItem[];
}

/**
 * One row in a mode composition: mandatory parts are read-only `[must]`; optional
 * user parts expose an inline RoleSelect. Selected parts (those entering
 * system_prompt) are visually emphasised.
 */
export function ModeCompositionRow({
  part,
  mode,
  optionalParts
}: ModeCompositionRowProps) {
  const [error, setError] = useState<string | null>(null);
  const optional =
    part.kind === "optional"
      ? optionalParts.find((p) => p.id === part.partId)
      : undefined;

  return (
    <li
      className={`flex flex-col gap-1 rounded-md border px-3 py-1.5 ${
        part.selected
          ? "border-primary/30 bg-primary/5"
          : "border-base-200 bg-base-100"
      }`}
    >
      <div className="flex items-center gap-2">
        <span className="flex w-5 justify-center">
          {part.selected ? (
            <Check
              aria-label="входит в system_prompt"
              size={14}
              strokeWidth={2.5}
              className="text-primary"
            />
          ) : (
            <span className="inline-block h-1.5 w-1.5 rounded-full bg-base-300" />
          )}
        </span>
        <span className="font-mono text-[13px] font-semibold text-base-content">
          {part.key}
        </span>
        <span className="text-[10px] font-bold uppercase tracking-wide text-base-content/40">
          {part.scope}
        </span>
        <div className="ml-auto">
          {part.kind === "optional" && optional ? (
            <RoleSelect
              part={optional}
              mode={mode}
              orderForNew={PROMPT_PART_MODES.indexOf(mode)}
              onError={setError}
            />
          ) : (
            <span className="inline-flex h-[18px] items-center gap-1 rounded-full bg-warning-soft px-2 text-[10px] font-bold uppercase tracking-wide text-warning">
              <Lock aria-hidden size={10} strokeWidth={2.5} />
              must
            </span>
          )}
        </div>
      </div>
      {error ? (
        <p role="alert" className="m-0 pl-7 text-xs text-error">
          {error}
        </p>
      ) : null}
    </li>
  );
}
