import { decisionMeta, type DreamProposalDecision } from "../model/types";

interface Props {
  decision: DreamProposalDecision;
}

export function DreamProposalDecisionBadge({ decision }: Props) {
  const meta = decisionMeta[decision];
  return (
    <span
      className="inline-flex h-[18px] items-center rounded-full px-2 text-[10px] font-bold uppercase tracking-wide"
      style={{ background: meta.surface, color: meta.ink }}
    >
      {meta.label}
    </span>
  );
}
