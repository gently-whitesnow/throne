import type { CSSProperties } from "react";

import type { IntentListItem } from "../model/types";

interface IntentApiCardProps {
  intent: IntentListItem;
}

interface CardStyle extends CSSProperties {
  "--intent-card-ink": string;
  "--intent-card-surface": string;
}

const FALLBACK_INK = "#187574";
const FALLBACK_SURFACE = "#c3faf5";

export function IntentApiCard({ intent }: IntentApiCardProps) {
  const updated = new Date(intent.updated_at).toLocaleDateString();
  const title = intent.text_short.split(/\r?\n/, 1)[0] || intent.id;

  return (
    <article
      className="intent-card"
      style={
        {
          "--intent-card-ink": FALLBACK_INK,
          "--intent-card-surface": FALLBACK_SURFACE
        } as CardStyle
      }
    >
      <header className="intent-card__header">
        <span className="intent-card__status">Intent</span>
      </header>
      <div>
        <h3 className="intent-card__title">{title}</h3>
        <p className="intent-card__summary">{intent.text_short}</p>
      </div>
      <ul className="intent-card__meta" aria-label="Метаданные intent">
        <li>v{intent.current_version}</li>
        <li>{updated}</li>
        {intent.tags.length > 0 ? <li>#{intent.tags.join(" #")}</li> : null}
      </ul>
    </article>
  );
}
