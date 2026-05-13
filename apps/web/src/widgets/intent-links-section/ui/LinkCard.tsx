import { ChevronDown, ChevronRight, ExternalLink, X } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";

import type { IntentLinkView } from "../model/types";
import { LinkExpandedView } from "./LinkExpandedView";

interface LinkCardProps {
  view: IntentLinkView;
  onDelete: () => void;
}

export function LinkCard({ view, onDelete }: LinkCardProps) {
  const [expanded, setExpanded] = useState(false);
  const { peer, link } = view;
  const title = peer.text_short.split(/\r?\n/, 1)[0] ?? peer.id;
  const snippet = snippetOf(peer.text_short);

  return (
    <li className="m-0 list-none">
      <article
        className={[
          "group flex flex-col gap-2 rounded-md border bg-base-100 transition-colors",
          expanded
            ? "border-primary/40 shadow-sm"
            : "border-base-300 hover:border-primary/30"
        ].join(" ")}
      >
        <div className="flex items-start gap-1.5 px-2.5 py-2">
          <button
            type="button"
            onClick={() => {
              setExpanded((v) => !v);
            }}
            aria-expanded={expanded}
            aria-label={expanded ? "Свернуть карточку" : "Развернуть карточку"}
            className="mt-0.5 inline-flex h-5 w-5 flex-shrink-0 items-center justify-center rounded text-base-content/40 hover:bg-base-200 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
          >
            {expanded ? (
              <ChevronDown size={14} aria-hidden />
            ) : (
              <ChevronRight size={14} aria-hidden />
            )}
          </button>
          <button
            type="button"
            onClick={() => {
              setExpanded((v) => !v);
            }}
            className="-mx-1 -my-0.5 flex min-w-0 flex-1 cursor-pointer flex-col gap-1 rounded px-1 py-0.5 text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
          >
            <span className="line-clamp-1 text-[12.5px] font-medium leading-snug text-base-content">
              {title}
            </span>
            {snippet && !expanded ? (
              <span className="line-clamp-2 text-[11.5px] leading-snug text-base-content/55">
                {snippet}
              </span>
            ) : null}
            {link.rationale && !expanded ? (
              <span className="line-clamp-1 text-[11px] italic leading-snug text-base-content/50">
                {link.rationale}
              </span>
            ) : null}
          </button>
          <div className="flex flex-shrink-0 items-start gap-0.5">
            <Link
              to={`/intents/${peer.id}`}
              title="Перейти к интенту"
              aria-label="Перейти к интенту"
              className="inline-flex h-6 w-6 items-center justify-center rounded text-base-content/40 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
            >
              <ExternalLink size={13} aria-hidden />
            </Link>
            <button
              type="button"
              onClick={onDelete}
              title="Удалить связь"
              aria-label="Удалить связь"
              className="inline-flex h-6 w-6 items-center justify-center rounded text-base-content/40 transition-colors hover:bg-error/10 hover:text-error focus-visible:outline-2 focus-visible:outline-error focus-visible:outline-offset-1"
            >
              <X size={13} aria-hidden />
            </button>
          </div>
        </div>
        {expanded && (
          <div className="border-t border-base-300 px-3 py-2.5">
            {link.rationale && (
              <p className="m-0 mb-2 text-[11.5px] italic text-base-content/60">
                {link.rationale}
              </p>
            )}
            <LinkExpandedView peerId={peer.id} />
          </div>
        )}
      </article>
    </li>
  );
}

function snippetOf(text: string): string {
  // Берём 2-3 строки после первой (заголовка), либо первую строку если она
  // была единственной. Текст уже усечён сервером в text_short, так что просто
  // склеиваем хвост одним пробелом для line-clamp-2.
  const lines = text.split(/\r?\n/);
  if (lines.length <= 1) return "";
  return lines
    .slice(1)
    .filter((l) => l.trim().length > 0)
    .slice(0, 3)
    .join(" ")
    .trim();
}
