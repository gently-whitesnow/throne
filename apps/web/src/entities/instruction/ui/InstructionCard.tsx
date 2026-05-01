import type { CSSProperties } from "react";

import { instructionKindLabel, type InstructionListItem } from "../model/types";

interface InstructionCardProps {
  instruction: InstructionListItem;
}

interface InstructionCardStyle extends CSSProperties {
  "--intent-card-ink": string;
  "--intent-card-surface": string;
}

export function InstructionCard({ instruction }: InstructionCardProps) {
  const meta = instructionKindLabel(instruction.kind);
  const updated = new Date(instruction.updated_at).toLocaleDateString();

  return (
    <article
      className="intent-card"
      style={
        {
          "--intent-card-ink": meta.ink,
          "--intent-card-surface": meta.surface
        } as InstructionCardStyle
      }
    >
      <header className="intent-card__header">
        <span className="intent-card__status">{meta.label}</span>
      </header>
      <div>
        <h3 className="intent-card__title">{instruction.kind}</h3>
        <p className="intent-card__summary">{instruction.text_short}</p>
      </div>
      <ul className="intent-card__meta" aria-label="Метаданные instruction">
        <li>v{instruction.current_version}</li>
        <li>{updated}</li>
      </ul>
    </article>
  );
}
