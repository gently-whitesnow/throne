import type { ReactNode } from "react";

import type { PromptPartPreview } from "../model/types";

import { PartChip, PartFrame } from "./PartFrame";

interface PreflightColumnProps {
  title: string;
  parts: PromptPartPreview[];
  onToggle: (partId: string) => void;
  onTextChange: (partId: string, text: string) => void;
  /** Контент перед рамками частей (зона задачи в USER-колонке). */
  children?: ReactNode;
}

/**
 * Колонка-поток частей system-промпта: включённые — рамками, доступные выключенные
 * — чипами внизу. Все части (любого scope) попадают в system-блок, который собирает
 * PromptCompositionResolver, поэтому делим колонки по факту (system / user), а не по
 * scope части.
 */
export function PreflightColumn({
  title,
  parts,
  onToggle,
  onTextChange,
  children
}: PreflightColumnProps) {
  const enabled = parts.filter((p) => p.selected);
  const available = parts.filter((p) => !p.selected);

  return (
    <section
      aria-label={title}
      className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto px-3 py-3"
    >
      <h4 className="m-0 text-xs font-semibold uppercase tracking-wide text-base-content/55">
        {title}
      </h4>
      {children}
      {enabled.map((part) => (
        <PartFrame
          key={part.part_id}
          part={part}
          onToggle={onToggle}
          onTextChange={onTextChange}
        />
      ))}
      {available.length > 0 ? (
        <div className="mt-1 flex flex-wrap gap-1.5">
          {available.map((part) => (
            <PartChip key={part.part_id} part={part} onEnable={onToggle} />
          ))}
        </div>
      ) : null}
    </section>
  );
}
