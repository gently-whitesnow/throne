import type { CSSProperties } from "react";

import type { IntentPreview } from "../model/types";
import { intentStatusMeta } from "../model/types";

interface IntentCardProps {
  intent: IntentPreview;
}

interface IntentCardStyle extends CSSProperties {
  "--intent-card-ink": string;
  "--intent-card-surface": string;
}

export function IntentCard({ intent }: IntentCardProps) {
  const status = intentStatusMeta[intent.status];

  return (
    <article
      className="intent-card"
      style={
        {
          "--intent-card-ink": status.ink,
          "--intent-card-surface": status.surface
        } as IntentCardStyle
      }
    >
      <header className="intent-card__header">
        <span className="intent-card__status">{status.label}</span>
      </header>
      <div>
        <h3 className="intent-card__title">{intent.title}</h3>
        <p className="intent-card__summary">{intent.summary}</p>
      </div>
      <ul className="intent-card__meta" aria-label="Метаданные intent">
        <li>v{intent.textVersion}</li>
        <li>{intent.updatedAt}</li>
      </ul>
    </article>
  );
}
