import {
  DreamReadinessStatusBadge,
  readinessStatusMeta
} from "@/entities/dream-readiness";

import { useDreamReadiness } from "../model/use-dream-readiness";

const tokensFormatter = new Intl.NumberFormat("en-US");

export function DreamFuelMeter() {
  const { state } = useDreamReadiness();

  if (state.kind === "loading") {
    return (
      <section className="rounded-lg border border-base-300 bg-base-100 p-4">
        <p className="m-0 text-sm text-base-content/60">Загрузка fuel meter…</p>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section className="rounded-lg border border-base-300 bg-base-100 p-4">
        <p role="alert" className="m-0 text-sm text-error">
          {state.message}
        </p>
      </section>
    );
  }

  const r = state.data;
  const meta = readinessStatusMeta[r.status];
  const hasLocked = r.locked_tokens > 0;

  return (
    <section
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
      aria-label="Dream context fuel meter"
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-2">
            <h3 className="m-0 text-base font-bold">Dream context</h3>
            <DreamReadinessStatusBadge status={r.status} />
          </div>
          <p className="m-0 text-xs text-base-content/60">{meta.description}</p>
        </div>
        <div className="text-right">
          <div className="font-mono text-2xl font-bold leading-none">
            {tokensFormatter.format(r.available_tokens)}
          </div>
          <div className="text-[11px] uppercase tracking-wide text-base-content/60">
            tokens · {String(r.intent_count)} intents
          </div>
        </div>
      </header>

      {hasLocked ? (
        <p className="m-0 text-xs text-base-content/60">
          <span className="font-semibold text-warning">
            {tokensFormatter.format(r.locked_tokens)} locked
          </span>{" "}
          в pending dream-run-ах. Сначала разберитесь с предложениями ниже.
        </p>
      ) : null}

      <p className="m-0 text-sm font-medium text-base-content">
        {r.suggested_action}
      </p>
    </section>
  );
}
