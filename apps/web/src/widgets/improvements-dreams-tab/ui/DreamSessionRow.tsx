import type { DreamSession } from "@/entities/dream-session";

export function DreamSessionRow({ session }: { session: DreamSession }) {
  const period = formatPeriod(session.date_from, session.date_to);
  const proposed = session.proposed_patch_ids.length;
  const processed = session.processed_conversation_ids.length;

  return (
    <li className="m-0 list-none rounded border border-base-300 bg-base-100 p-3">
      <header className="flex items-center gap-2 text-xs">
        <span className="font-semibold uppercase tracking-wide">
          {session.vendor}
        </span>
        <span className="text-base-content/60">{period}</span>
        <span className="ml-auto text-base-content/60">
          {new Date(session.created_at).toLocaleString()}
        </span>
      </header>
      <p className="m-0 mt-2 whitespace-pre-line text-sm leading-relaxed text-base-content/80">
        {session.summary}
      </p>
      {session.reflection ? (
        <details className="mt-2">
          <summary className="cursor-pointer text-xs font-medium text-base-content/70">
            Рефлексия по прошлым патчам
          </summary>
          <p className="m-0 mt-1 whitespace-pre-line text-xs leading-relaxed text-base-content/70">
            {session.reflection}
          </p>
        </details>
      ) : null}
      <footer className="mt-2 flex flex-wrap gap-3 text-[11px] text-base-content/60">
        <span>
          Прочитано диалогов: <strong>{processed}</strong>
        </span>
        <span>
          Предложено патчей: <strong>{proposed}</strong>
        </span>
      </footer>
    </li>
  );
}

function formatPeriod(from?: string | null, to?: string | null): string {
  if (!from && !to) return "период не задан";
  const f = from ? new Date(from).toLocaleDateString() : "…";
  const t = to ? new Date(to).toLocaleDateString() : "…";
  return `${f} – ${t}`;
}
