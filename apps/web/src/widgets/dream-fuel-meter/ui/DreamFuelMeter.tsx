import {
  DreamReadinessStatusBadge,
  readinessStatusMeta
} from "@/entities/dream-readiness";
import { DreamEvidenceCountsList } from "@/entities/dream-run";

import { useDreamReadiness } from "../model/use-dream-readiness";

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
  const showsLocked = r.status === "pending_review" || r.locked_score > 0;
  const total = Math.max(r.threshold, r.available_score + r.locked_score);
  const availablePct =
    total > 0 ? Math.min(100, (r.available_score / total) * 100) : 0;
  const lockedPct =
    total > 0 ? Math.min(100, (r.locked_score / total) * 100) : 0;

  return (
    <section
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
      aria-label="Dream fuel meter"
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-2">
            <h3 className="m-0 text-base font-bold">Dream fuel</h3>
            <DreamReadinessStatusBadge status={r.status} />
          </div>
          <p className="m-0 text-xs text-base-content/60">{meta.description}</p>
        </div>
        <div className="text-right">
          <div className="font-mono text-2xl font-bold leading-none">
            {String(r.available_score)}
          </div>
          <div className="text-[11px] uppercase tracking-wide text-base-content/60">
            available · threshold {String(r.threshold)}
          </div>
        </div>
      </header>

      <div
        className="relative h-2 w-full overflow-hidden rounded-full bg-base-200"
        aria-label="Score bar"
      >
        <div
          className="absolute inset-y-0 left-0 bg-success"
          style={{ width: `${String(availablePct)}%` }}
        />
        {showsLocked ? (
          <div
            className="absolute inset-y-0 bg-warning/70"
            style={{
              left: `${String(availablePct)}%`,
              width: `${String(lockedPct)}%`
            }}
          />
        ) : null}
      </div>

      {showsLocked ? (
        <p className="m-0 text-xs text-base-content/60">
          <span className="font-semibold text-warning">
            {String(r.locked_score)} locked
          </span>{" "}
          в pending dream-run-ах. Сначала разберитесь с предложениями ниже.
        </p>
      ) : null}

      <DreamEvidenceCountsList counts={r.evidence_counts} />

      <p className="m-0 text-sm font-medium text-base-content">
        {r.suggested_action}
      </p>
    </section>
  );
}
