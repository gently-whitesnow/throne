import { useState } from "react";

import {
  type DreamRun,
  type DreamRunDetail,
  DreamRunStatusBadge,
  isEmptyRun,
  pendingProposalsCount
} from "@/entities/dream-run";
import { CloseDreamRunModal } from "@/features/close-dream-run";

import { useDreamRunDetail } from "../model/use-dream-run-detail";
import { DreamProposalCard } from "./DreamProposalCard";
import { usePendingDreamRuns } from "../model/use-pending-dream-runs";

const tokensFormatter = new Intl.NumberFormat("en-US");

export function DreamRunsList() {
  const { state } = usePendingDreamRuns();
  const [expandedRunId, setExpandedRunId] = useState<string | null>(null);

  return (
    <section className="flex flex-col gap-3" aria-label="Pending dream runs">
      <header className="flex items-baseline justify-between gap-2">
        <h3 className="m-0 text-base font-bold">Pending runs</h3>
        {state.kind === "ready" ? (
          <span className="text-xs text-base-content/60">
            {String(state.items.length)} run(s)
          </span>
        ) : null}
      </header>

      {state.kind === "loading" && (
        <p className="m-0 text-sm text-base-content/60">Загрузка…</p>
      )}
      {state.kind === "error" && (
        <p role="alert" className="m-0 text-sm text-error">
          {state.message}
        </p>
      )}
      {state.kind === "ready" && state.items.length === 0 && (
        <div className="rounded-lg border border-dashed border-base-300 p-6 text-center">
          <p className="m-0 text-sm text-base-content/60">
            Нет открытых dream-runs.
          </p>
        </div>
      )}
      {state.kind === "ready" && state.items.length > 0 && (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
          {state.items.map((run) => (
            <li key={run.id}>
              <DreamRunRow
                run={run}
                expanded={expandedRunId === run.id}
                onToggle={() => {
                  setExpandedRunId((prev) => (prev === run.id ? null : run.id));
                }}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

interface RowProps {
  run: DreamRun;
  expanded: boolean;
  onToggle: () => void;
}

function DreamRunRow({ run, expanded, onToggle }: RowProps) {
  const pending = pendingProposalsCount(run);
  const empty = isEmptyRun(run);

  return (
    <article className="rounded-lg border border-base-300 bg-base-100 p-4">
      <header className="flex flex-wrap items-center gap-2">
        <DreamRunStatusBadge status={run.status} />
        <span className="text-sm font-semibold">
          {tokensFormatter.format(run.token_count)} tokens
        </span>
        <span className="text-xs text-base-content/60">
          · {String(run.intent_refs.length)} intents
        </span>
        <span className="text-xs text-base-content/60">
          · {String(run.proposals.length)} proposals ({String(pending)} pending)
        </span>
        <span className="ml-auto text-[11px] text-base-content/60">
          {formatDate(run.created_at)}
        </span>
      </header>
      <div className="mt-2 flex gap-2">
        <button
          type="button"
          className="text-xs font-semibold text-primary hover:underline"
          onClick={onToggle}
        >
          {expanded ? "Свернуть" : "Развернуть proposals"}
        </button>
      </div>
      {expanded ? <DreamRunDetailPanel runId={run.id} empty={empty} /> : null}
    </article>
  );
}

function DreamRunDetailPanel({
  runId,
  empty
}: {
  runId: string;
  empty: boolean;
}) {
  const { state, setData } = useDreamRunDetail(runId);
  const [closing, setClosing] = useState(false);

  const handleProposalChanged = (run: DreamRun) => {
    if (state?.kind === "ready") {
      setData({ run, previews: state.data.previews });
    }
  };

  if (!state || state.kind === "loading") {
    return (
      <p className="mt-3 text-sm text-base-content/60">Загрузка proposals…</p>
    );
  }
  if (state.kind === "error") {
    return (
      <p role="alert" className="mt-3 text-sm text-error">
        {state.message}
      </p>
    );
  }

  const detail: DreamRunDetail = state.data;
  const previewsByProposal = new Map(
    detail.previews.map((p) => [p.proposal_id, p])
  );
  const showCloseButton = empty || detail.run.proposals.length === 0;

  return (
    <div className="mt-4 flex flex-col gap-3">
      {detail.run.proposals.length === 0 ? (
        <p className="m-0 text-sm text-base-content/60">
          В этом run нет proposals — агент не нашёл достойных правил.
        </p>
      ) : (
        detail.run.proposals.map((proposal) => (
          <DreamProposalCard
            key={proposal.id}
            runId={runId}
            proposal={proposal}
            preview={previewsByProposal.get(proposal.id)}
            onChanged={handleProposalChanged}
          />
        ))
      )}

      {showCloseButton ? (
        <button
          type="button"
          className="btn btn-sm btn-soft self-start"
          onClick={() => {
            setClosing(true);
          }}
        >
          {empty ? "Discard run" : "Force close"}
        </button>
      ) : null}

      {closing ? (
        <CloseDreamRunModal
          run={detail.run}
          onClosed={(run) => {
            setClosing(false);
            setData({ run, previews: detail.previews });
          }}
          onCancel={() => {
            setClosing(false);
          }}
        />
      ) : null}
    </div>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}
