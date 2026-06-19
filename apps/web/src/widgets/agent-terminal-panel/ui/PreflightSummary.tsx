import { ChevronRight } from "lucide-react";
import { useId, useState } from "react";

interface PreflightSummaryProps {
  systemPrompt: string;
  userPrompt: string;
  freeInput: string;
}

function SummaryBlock({ label, value }: { label: string; value: string }) {
  const text = value.trim();
  return (
    <div className="flex flex-col gap-1">
      <span className="text-[11px] font-semibold uppercase tracking-wide text-base-content/45">
        {label}
      </span>
      {text.length > 0 ? (
        <pre className="m-0 max-h-48 overflow-auto whitespace-pre-wrap break-words rounded border border-base-300 bg-base-200/40 px-2 py-1.5 font-mono text-[11px] leading-snug text-base-content/80">
          {value}
        </pre>
      ) : (
        <p className="m-0 rounded border border-dashed border-base-300 px-2 py-1.5 text-[11px] text-base-content/40">
          пусто
        </p>
      )}
    </div>
  );
}

/**
 * Сворачиваемое превью собранного итога (system / user / free) — то, что реально
 * уйдёт агенту. Пересобирается живьём из текущих частей и зоны задачи. По
 * умолчанию свёрнуто, чтобы не съедать высоту, пока оператор правит части.
 */
export function PreflightSummary({
  systemPrompt,
  userPrompt,
  freeInput
}: PreflightSummaryProps) {
  const [open, setOpen] = useState(false);
  const bodyId = useId();

  return (
    <section className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100">
      <button
        type="button"
        onClick={() => {
          setOpen((value) => !value);
        }}
        aria-expanded={open}
        aria-controls={bodyId}
        className="flex items-center gap-1.5 px-2 py-1.5 text-left"
      >
        <ChevronRight
          aria-hidden
          size={13}
          strokeWidth={2.5}
          className={`flex-shrink-0 text-base-content/40 transition-transform ${
            open ? "rotate-90" : ""
          }`}
        />
        <span className="text-xs font-semibold uppercase tracking-wide text-base-content/55">
          Итог — что уйдёт агенту
        </span>
      </button>
      {open ? (
        <div
          id={bodyId}
          data-testid="agent-terminal-preflight-summary"
          className="flex flex-col gap-3 px-2 pb-2"
        >
          <SummaryBlock label="system" value={systemPrompt} />
          <SummaryBlock label="user" value={userPrompt} />
          <SummaryBlock label="free" value={freeInput} />
        </div>
      ) : null}
    </section>
  );
}
