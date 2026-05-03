import { useState } from "react";

import {
  type DreamProposal,
  type DreamProposalPreview,
  DreamProposalDecisionBadge,
  DreamProposalSeverityBadge
} from "@/entities/dream-proposal";
import { type DreamIntentRef, type DreamRun } from "@/entities/dream-run";
import { ApplyDreamProposalModal } from "@/features/apply-dream-proposal";
import { SkipDreamProposalModal } from "@/features/skip-dream-proposal";
import { Button } from "@/shared/ui";

interface Props {
  runId: string;
  proposal: DreamProposal;
  preview?: DreamProposalPreview;
  onChanged: (run: DreamRun) => void;
}

type ModalState = "none" | "apply" | "skip";

export function DreamProposalCard({
  runId,
  proposal,
  preview,
  onChanged
}: Props) {
  const [modal, setModal] = useState<ModalState>("none");
  const [showDiff, setShowDiff] = useState(false);

  const close = () => {
    setModal("none");
  };
  const handleChanged = (run: DreamRun) => {
    setModal("none");
    onChanged(run);
  };

  const isPending = proposal.decision === "pending";

  return (
    <article className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-100 p-4">
      <header className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-xs uppercase tracking-wide text-base-content/70">
          {proposal.target_kind}
        </span>
        <DreamProposalSeverityBadge severity={proposal.severity} />
        <DreamProposalDecisionBadge decision={proposal.decision} />
        {preview && !preview.base_version_matches_current ? (
          <span className="inline-flex h-[18px] items-center rounded-full bg-error-soft px-2 text-[10px] font-bold uppercase tracking-wide text-error">
            needs rebase
          </span>
        ) : null}
        <span className="ml-auto text-[11px] text-base-content/60">
          base v{String(proposal.base_instruction_version)}
          {proposal.applied_instruction_version
            ? ` → v${String(proposal.applied_instruction_version)}`
            : ""}
        </span>
      </header>

      <div className="rounded bg-base-200 p-3 font-mono text-[13px] leading-relaxed">
        {proposal.final_rule ?? proposal.proposed_rule}
      </div>

      {proposal.rationale ? (
        <p className="m-0 text-sm leading-relaxed text-base-content/80">
          {proposal.rationale}
        </p>
      ) : null}

      {proposal.evidence_summary ? (
        <p className="m-0 text-xs italic text-base-content/60">
          {proposal.evidence_summary}
        </p>
      ) : null}

      {proposal.intent_refs.length > 0 ? (
        <IntentRefsList refs={proposal.intent_refs} />
      ) : null}

      {proposal.rejected_reason ? (
        <div className="rounded border border-base-300 bg-base-200/60 p-2 text-xs">
          <span className="font-semibold">Skipped:</span>{" "}
          {proposal.rejected_reason}
        </div>
      ) : null}

      {preview ? (
        <div className="flex flex-col gap-2">
          <button
            type="button"
            className="self-start text-xs font-semibold text-primary hover:underline"
            onClick={() => {
              setShowDiff((s) => !s);
            }}
          >
            {showDiff ? "Скрыть preview" : "Показать preview"}
          </button>
          {showDiff ? <PreviewDiff preview={preview} /> : null}
        </div>
      ) : null}

      {isPending && preview ? (
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="primary"
            onClick={() => {
              setModal("apply");
            }}
            disabled={!preview.base_version_matches_current}
          >
            Apply
          </Button>
          <Button
            type="button"
            onClick={() => {
              setModal("skip");
            }}
          >
            Skip
          </Button>
        </div>
      ) : null}

      {modal === "apply" && preview ? (
        <ApplyDreamProposalModal
          runId={runId}
          proposal={proposal}
          baseVersionMatchesCurrent={preview.base_version_matches_current}
          currentInstructionVersion={preview.current_instruction_version}
          onApplied={handleChanged}
          onClose={close}
        />
      ) : null}
      {modal === "skip" ? (
        <SkipDreamProposalModal
          runId={runId}
          proposal={proposal}
          onSkipped={handleChanged}
          onClose={close}
        />
      ) : null}
    </article>
  );
}

const tokensFormatter = new Intl.NumberFormat("en-US");

function IntentRefsList({ refs }: { refs: DreamIntentRef[] }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {refs.map((ref) => (
        <span
          key={ref.intent_id}
          className="inline-flex h-[18px] items-center gap-1 rounded-full bg-base-200 px-2 text-[10px] font-semibold uppercase tracking-wide text-base-content/70"
          title={ref.intent_id}
        >
          <span className="font-mono normal-case">
            {shortId(ref.intent_id)}
          </span>
          <span>· {tokensFormatter.format(ref.token_count)} tok</span>
        </span>
      ))}
    </div>
  );
}

function PreviewDiff({ preview }: { preview: DreamProposalPreview }) {
  return (
    <div className="grid gap-2 md:grid-cols-2">
      <pre className="m-0 max-h-72 overflow-auto rounded border border-base-300 bg-base-200/60 p-2 font-mono text-[11px] leading-relaxed">
        <span className="block text-[10px] uppercase tracking-wide text-base-content/60">
          current v{String(preview.current_instruction_version)}
        </span>
        {preview.current_text}
      </pre>
      <pre className="m-0 max-h-72 overflow-auto rounded border border-success/40 bg-success-soft/40 p-2 font-mono text-[11px] leading-relaxed">
        <span className="block text-[10px] uppercase tracking-wide text-base-content/60">
          proposed
        </span>
        {preview.proposed_text}
      </pre>
    </div>
  );
}

function shortId(id: string): string {
  if (id.length <= 8) return id;
  return id.slice(0, 8);
}
