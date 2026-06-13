import { Plus } from "lucide-react";
import { useState } from "react";

import type { PromptPartListItem } from "@/entities/prompt-part";
import { Button } from "@/shared/ui";

import type { ProjectedInstruction } from "../model/composition";
import { InstructionRow, OptionalRow } from "./PartsListRow";

interface PartsListProps {
  instructions: ProjectedInstruction[];
  optionalParts: PromptPartListItem[];
  onOpenInstruction: (instruction: ProjectedInstruction) => void;
  onOpenOptional: (part: PromptPartListItem) => void;
  onCreateOptional: () => void;
}

/**
 * Unified parts list: projected mandatory instructions (read-only / editable
 * user instructions) and operator-authored optional parts with per-mode role
 * controls.
 */
export function PartsList({
  instructions,
  optionalParts,
  onOpenInstruction,
  onOpenOptional,
  onCreateOptional
}: PartsListProps) {
  const [roleError, setRoleError] = useState<string | null>(null);

  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-2">
        <h3 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/70">
          Инструкции (mandatory)
        </h3>
        <p className="m-0 text-xs text-base-content/60">
          Спроецированы из манифеста бандлов. System read-only, user
          редактируются. Всегда обязательны в своих режимах.
        </p>
        {instructions.length === 0 ? (
          <p className="m-0 text-[13px] text-base-content/60">
            Нет инструкций.
          </p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
            {instructions.map((instruction) => (
              <InstructionRow
                key={`${instruction.scope}:${instruction.key}`}
                instruction={instruction}
                onOpen={() => {
                  onOpenInstruction(instruction);
                }}
              />
            ))}
          </ul>
        )}
      </section>

      <section className="flex flex-col gap-2">
        <div className="flex items-center justify-between gap-2">
          <h3 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/70">
            Optional-части
          </h3>
          <Button
            variant="primary"
            icon={<Plus aria-hidden size={14} strokeWidth={2.5} />}
            onClick={onCreateOptional}
          >
            Создать optional-часть
          </Button>
        </div>
        {roleError ? (
          <p role="alert" className="m-0 text-sm text-error">
            {roleError}
          </p>
        ) : null}
        {optionalParts.length === 0 ? (
          <p className="m-0 text-[13px] text-base-content/60">
            Пока нет optional-частей. Создайте первую.
          </p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
            {optionalParts.map((part) => (
              <OptionalRow
                key={part.id}
                part={part}
                onOpen={() => {
                  onOpenOptional(part);
                }}
                onRoleError={setRoleError}
              />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
